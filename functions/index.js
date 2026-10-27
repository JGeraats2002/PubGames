/**
 * Pub Games payments, on the server. The app never decides itself that
 * something is bought: it hands Google Play's purchase token to these
 * functions, which check it with Google and only then write what the user
 * owns to entitlements/{uid} (which the app can read but not write).
 *
 * - verifyPurchase: after buying a game or the pass in the app (and when
 *   restoring purchases on a new phone).
 * - refreshPass: when the pass has run out on this phone's last check - was
 *   it renewed (or cancelled)?
 * - syncGameProduct: when an admin saves a paid game, its product and price
 *   are made or updated in the Play Console, so nothing has to be done there
 *   by hand per game.
 * - todaysCode / renewCode / redeemCode: the owner's friend code - shown
 *   only to the owner, working 15 minutes (30 after New code) - unlocks a
 *   game for free (100% off), without Google Play: no money changes hands.
 *   Not the pass.
 * - removeRefunds: once a day, takes away what Google refunded or charged back.
 *
 * Everything stays well inside Firebase's free usage.
 */
const { onCall, HttpsError } = require("firebase-functions/v2/https");
const { onDocumentWritten } = require("firebase-functions/v2/firestore");
const { onSchedule } = require("firebase-functions/v2/scheduler");
const { setGlobalOptions, logger } = require("firebase-functions/v2");
const admin = require("firebase-admin");
const { google } = require("googleapis");
const crypto = require("crypto");

admin.initializeApp();
const db = admin.firestore();

// Close to the players (and their data). Each server handles up to 40 requests at the same time and
// there are at most 5: 200 at once, far more than a pub game needs - and a limit, so no surprise costs.
// Busier than that, a request waits a moment for a free spot (the app tries once more).
setGlobalOptions({ region: "europe-west1", maxInstances: 5, concurrency: 40, cpu: 1, memory: "512MiB" });

const OWNER_EMAILS = ["jjh.geraats@gmail.com"];
const CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

const PACKAGE_NAME = "com.pubgems.pubgames";
const PASS_PRODUCT_ID = "pubgames_pass";

/** Google Play's product id for a game: "game_" and its id, as Play allows (lower case, digits, _ and .). */
function productIdForGame(gameId) {
  return "game_" + String(gameId).toLowerCase().replace(/[^a-z0-9_.]/g, "_");
}

/** The Play Developer API, signed in as this project's service account (which the Play Console must allow). */
async function publisher() {
  const auth = new google.auth.GoogleAuth({ scopes: ["https://www.googleapis.com/auth/androidpublisher"] });
  return google.androidpublisher({ version: "v3", auth: await auth.getClient() });
}

/** A purchase token belongs to one account only: the first one it's used for. */
async function claimToken(token, uid, productId) {
  const ref = db.collection("purchaseTokens").doc(crypto.createHash("sha256").update(token).digest("hex"));
  await db.runTransaction(async (tx) => {
    const doc = await tx.get(ref);
    if (doc.exists && doc.data().uid !== uid)
      throw new HttpsError("permission-denied", "This purchase belongs to another account.");
    if (!doc.exists)
      tx.set(ref, { uid, productId, at: admin.firestore.FieldValue.serverTimestamp() });
  });
}

async function verifyGame(api, uid, gameId, token) {
  const productId = productIdForGame(gameId);
  const { data } = await api.purchases.products.get({ packageName: PACKAGE_NAME, productId, token });
  // 0 = purchased (1 cancelled, 2 pending).
  if (data.purchaseState !== 0)
    throw new HttpsError("failed-precondition", data.purchaseState === 2 ? "The payment is still pending." : "This purchase was cancelled.");
  if (data.obfuscatedExternalAccountId && data.obfuscatedExternalAccountId !== uid)
    throw new HttpsError("permission-denied", "This purchase belongs to another account.");
  await claimToken(token, uid, productId);
  if (data.acknowledgementState === 0)
    await api.purchases.products.acknowledge({ packageName: PACKAGE_NAME, productId, token, requestBody: {} });
  await db.collection("entitlements").doc(uid).set(
    { games: admin.firestore.FieldValue.arrayUnion(String(gameId)), updatedAt: admin.firestore.FieldValue.serverTimestamp() },
    { merge: true });
}

/** Checks a pass token with Google and writes until when the pass runs (in the past when it's over). */
async function applyPass(api, uid, token) {
  const { data } = await api.purchases.subscriptionsv2.get({ packageName: PACKAGE_NAME, token });
  const ownerId = data.externalAccountIdentifiers && data.externalAccountIdentifiers.obfuscatedExternalAccountId;
  if (ownerId && ownerId !== uid)
    throw new HttpsError("permission-denied", "This subscription belongs to another account.");
  await claimToken(token, uid, PASS_PRODUCT_ID);

  const expiries = (data.lineItems || []).map((item) => Date.parse(item.expiryTime)).filter((t) => !isNaN(t));
  const expiresAt = expiries.length ? new Date(Math.max(...expiries)) : new Date(0);
  const active = ["SUBSCRIPTION_STATE_ACTIVE", "SUBSCRIPTION_STATE_IN_GRACE_PERIOD"].includes(data.subscriptionState);
  if (active && data.acknowledgementState === "ACKNOWLEDGEMENT_STATE_PENDING")
    await api.purchases.subscriptions.acknowledge({ packageName: PACKAGE_NAME, subscriptionId: PASS_PRODUCT_ID, token, requestBody: {} });

  // Cancelled but paid for: it still runs until its expiry.
  const passUntil = active || data.subscriptionState === "SUBSCRIPTION_STATE_CANCELED" ? expiresAt : new Date(0);
  const plan = (data.lineItems || [])[0];
  await db.collection("entitlements").doc(uid).set({
    passUntil: admin.firestore.Timestamp.fromDate(passUntil),
    passPlan: plan && plan.offerDetails ? plan.offerDetails.basePlanId || null : null,
    passRenews: data.subscriptionState === "SUBSCRIPTION_STATE_ACTIVE",
    updatedAt: admin.firestore.FieldValue.serverTimestamp()
  }, { merge: true });
  // Kept on the server only, to check again later (refreshPass).
  await db.collection("subscriptions").doc(uid).set({ token, productId: PASS_PRODUCT_ID });
  return passUntil;
}

/** { kind: "game", gameId, purchaseToken } or { kind: "pass", purchaseToken }. */
exports.verifyPurchase = onCall(async (request) => {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Sign in first.");
  const { kind, gameId, purchaseToken } = request.data || {};
  if (typeof purchaseToken !== "string" || purchaseToken.length === 0 || purchaseToken.length > 4096)
    throw new HttpsError("invalid-argument", "No purchase to check.");

  const api = await publisher();
  try {
    if (kind === "game") {
      if (typeof gameId !== "string" || gameId.length === 0) throw new HttpsError("invalid-argument", "Which game?");
      await verifyGame(api, uid, gameId, purchaseToken);
      return { ok: true };
    }
    if (kind === "pass") {
      const until = await applyPass(api, uid, purchaseToken);
      return { ok: true, passUntil: until.toISOString() };
    }
    throw new HttpsError("invalid-argument", "Unknown purchase.");
  } catch (error) {
    if (error instanceof HttpsError) throw error;
    logger.error("verifyPurchase failed", error);
    throw new HttpsError("internal", "The purchase couldn't be checked. Try Restore purchases later.");
  }
});

/** The pass ran out on the phone's last check: ask Google whether it was renewed. */
exports.refreshPass = onCall(async (request) => {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Sign in first.");
  const known = await db.collection("subscriptions").doc(uid).get();
  if (!known.exists) return { ok: true, passUntil: null };
  try {
    const until = await applyPass(await publisher(), uid, known.data().token);
    return { ok: true, passUntil: until.toISOString() };
  } catch (error) {
    if (error instanceof HttpsError) throw error;
    logger.error("refreshPass failed", error);
    throw new HttpsError("internal", "The subscription couldn't be checked right now.");
  }
});

/**
 * A game was saved: when it's paid (and published), its product in the Play
 * Console gets the game's name and price; when it's free again or deleted,
 * the product is switched off. Prices are the app's fixed price levels.
 */
exports.syncGameProduct = onDocumentWritten("games/{gameId}", async (event) => {
  const after = event.data && event.data.after && event.data.after.exists ? event.data.after.data() : null;
  const before = event.data && event.data.before && event.data.before.exists ? event.data.before.data() : null;
  const gameId = event.params.gameId;
  const sku = productIdForGame(gameId);

  const sellable = (g) => g && g.isPaid === true && g.status === "Published" && typeof g.price === "number" && g.price > 0;
  const wasSellable = sellable(before);
  const isSellable = sellable(after);
  if (!wasSellable && !isSellable) return;
  if (wasSellable && isSellable && before.price === after.price && before.name === after.name) return;

  const api = await publisher();
  try {
    if (isSellable) {
      await api.inappproducts.patch({
        packageName: PACKAGE_NAME,
        sku,
        allowMissing: true,
        autoConvertMissingPrices: true,
        requestBody: {
          packageName: PACKAGE_NAME,
          sku,
          status: "active",
          purchaseType: "managedUser",
          defaultLanguage: "en-US",
          defaultPrice: { priceMicros: String(Math.round(after.price * 1000000)), currency: "EUR" },
          listings: { "en-US": { title: String(after.name).slice(0, 55), description: `Unlock ${String(after.name).slice(0, 60)} in Pub Games, for good.` } }
        }
      });
    } else {
      await api.inappproducts.patch({
        packageName: PACKAGE_NAME,
        sku,
        requestBody: { packageName: PACKAGE_NAME, sku, status: "inactive" }
      });
    }
  } catch (error) {
    // Logged for the developer; the game itself is saved either way.
    logger.error(`syncGameProduct ${sku} failed`, error);
  }
});


// ----- The owner's friend code -----
//
// There's one code at a time, kept in serverConfig/friendCode (no app access).
// A code works for 15 minutes; when the owner looks after that, a new one is
// made. With New code the owner makes one right away that works for 30
// minutes - the old one stops working at once.

const CODE_MINUTES = 15;
const RENEWED_CODE_MINUTES = 30;

function isOwner(request) {
  const token = request.auth && request.auth.token;
  return !!token && token.email_verified === true && OWNER_EMAILS.includes(String(token.email).toLowerCase());
}

/** 8 random characters without look-alikes (no 0/O, 1/I/L), like "K7QM3XPA". */
function newCode() {
  let code = "";
  for (let i = 0; i < 8; i++) code += CODE_ALPHABET[crypto.randomInt(CODE_ALPHABET.length)];
  return code;
}

const friendCodeRef = () => db.collection("serverConfig").doc("friendCode");

/** Saves a new code that works for this many minutes, and returns it. */
async function makeCode(minutes) {
  const code = newCode();
  const validUntil = new Date(Date.now() + minutes * 60 * 1000);
  await friendCodeRef().set({ code, validUntil: admin.firestore.Timestamp.fromDate(validUntil) });
  return { code, validUntil };
}

/** The code that works now, or null. */
async function currentCode() {
  const doc = await friendCodeRef().get();
  if (!doc.exists) return null;
  const { code, validUntil } = doc.data();
  const until = validUntil && validUntil.toDate();
  return code && until && until > new Date() ? { code, validUntil: until } : null;
}

const shown = ({ code, validUntil }) => ({ code: `${code.slice(0, 4)}-${code.slice(4)}`, validUntil: validUntil.toISOString() });

/** Today in the Netherlands, e.g. "2026-10-27": the limit on wrong codes is per day. */
function today() {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Amsterdam" }).format(new Date());
}

/** The owner's code now (a new one when the last ran out), and until when it works. */
exports.todaysCode = onCall(async (request) => {
  if (!isOwner(request)) throw new HttpsError("permission-denied", "Only the owner sees the friend code.");
  return shown((await currentCode()) || (await makeCode(CODE_MINUTES)));
});

/** New code: the old one stops working; the new one works for 30 minutes. */
exports.renewCode = onCall(async (request) => {
  if (!isOwner(request)) throw new HttpsError("permission-denied", "Only the owner can make a new friend code.");
  return shown(await makeCode(RENEWED_CODE_MINUTES));
});

/**
 * { code, gameId }: with the owner's current friend code the game is
 * unlocked for good (100% off). Only games - not the pass. Wrong codes are
 * limited to 10 a day per account, so codes can't be guessed.
 */
exports.redeemCode = onCall(async (request) => {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Sign in first.");
  const { code, gameId } = request.data || {};
  if (typeof gameId !== "string" || gameId.length === 0) throw new HttpsError("invalid-argument", "Which game?");
  const typed = String(code || "").toUpperCase().replace(/[^A-Z0-9]/g, "");

  const attempts = db.collection("codeAttempts").doc(`${uid}_${today()}`);
  const tried = await attempts.get();
  if (tried.exists && tried.data().wrong >= 10)
    throw new HttpsError("resource-exhausted", "Too many wrong codes today. Try again tomorrow.");

  const current = await currentCode();
  if (!current || typed !== current.code) {
    await attempts.set({ wrong: admin.firestore.FieldValue.increment(1) }, { merge: true });
    throw new HttpsError("invalid-argument", "This code isn't valid (any more). Ask for the current one.");
  }

  await db.collection("entitlements").doc(uid).set(
    { games: admin.firestore.FieldValue.arrayUnion(gameId), updatedAt: admin.firestore.FieldValue.serverTimestamp() },
    { merge: true });
  return { ok: true };
});

// ----- Refunds -----

/**
 * Once a day: purchases Google refunded or charged back are taken away again
 * (a game off the list, a pass ended). Google lists them for 30 days.
 */
exports.removeRefunds = onSchedule({ schedule: "every day 04:00", timeZone: "Europe/Amsterdam" }, async () => {
  const api = await publisher();
  const since = Date.now() - 2 * 24 * 60 * 60 * 1000; // a little overlap, so nothing is missed between runs
  let pageToken;
  do {
    const { data } = await api.purchases.voidedpurchases.list({ packageName: PACKAGE_NAME, startTime: String(since), type: 1, token: pageToken });
    for (const voided of data.voidedPurchases || []) {
      const hash = crypto.createHash("sha256").update(voided.purchaseToken).digest("hex");
      const claim = await db.collection("purchaseTokens").doc(hash).get();
      if (!claim.exists) continue;
      const { uid, productId } = claim.data();
      const entitlements = db.collection("entitlements").doc(uid);
      if (productId === PASS_PRODUCT_ID) {
        await entitlements.set({ passUntil: admin.firestore.Timestamp.fromDate(new Date(0)), passRenews: false }, { merge: true });
      } else if (productId.startsWith("game_")) {
        // Which game: the paid game whose product id this is.
        const games = await db.collection("games").where("isPaid", "==", true).get();
        const game = games.docs.find((g) => productIdForGame(g.id) === productId);
        if (game) await entitlements.set({ games: admin.firestore.FieldValue.arrayRemove(game.id) }, { merge: true });
      }
      logger.info(`Refunded purchase taken away: ${productId} for ${uid}`);
    }
    pageToken = data.tokenPagination && data.tokenPagination.nextPageToken;
  } while (pageToken);
});
