/**
 * Small rules the Cloud Functions (index.js) use, kept apart so they can be tested without Firebase
 * (test/shared.test.js, run with npm test). Several must give exactly what the app gives; the tests check
 * both sides with the same examples (tests/PubGames.Tests in the repo).
 */
const crypto = require("crypto");

// The app's language codes, and what Cloud Translation calls them.
const LANGUAGES = { en: "en", nl: "nl", fr: "fr", de: "de", es: "es", it: "it", pt: "pt-BR", zh: "zh-CN", ar: "ar" };

// A picture in the rules: ![image](reference). Pictures are kept as they are; only the text around them is translated.
const IMAGE_TAG = /!\[[^\]]*\]\([^)\s]+\)/g;

/** Which version of a game's name and rules a translation is of. Must match GameTranslation.HashOf in the app. */
function sourceHash(name, rulesText, cover) {
  return crypto.createHash("sha256").update(`${name}\u001f${rulesText}\u001f${cover || ""}`, "utf8").digest("hex").slice(0, 16);
}

/** The app's language code for a code from Cloud Translation (pt-BR -> pt, zh-CN -> zh), or null. */
function appLanguageOf(code) {
  if (typeof code !== "string") return null;
  const base = code.toLowerCase().split("-")[0];
  return LANGUAGES[base] ? base : null;
}

/**
 * A text in a picture that stays as it is in every language: empty, the game's name (a name isn't
 * translated), or without any letters (numbers, symbols).
 */
function keepsPictureText(text, gameName) {
  const trimmed = text.trim();
  return trimmed.length === 0
    || trimmed.toLowerCase() === String(gameName || "").trim().toLowerCase()
    || !/\p{L}/u.test(trimmed);
}

// DrawKind.Hand in the app.
const DRAW_KIND_HAND = 4;

/**
 * A hand showing only the middle finger (thumb either way): never allowed in a picture. An item as the app
 * saves it in a drawing (Kind, Neutral, Fingers). Must match DrawRules.IsRudeHand in the app.
 */
function isRudeHand(item) {
  const fingers = item.Fingers;
  return item.Kind === DRAW_KIND_HAND && item.Neutral === false && Array.isArray(fingers) && fingers.length === 5
    && fingers[2] === true && !fingers[1] && !fingers[3] && !fingers[4];
}

/**
 * TRANSLATIONS PER SENTENCE. A game's rules are cut into segments the way translation tools do it: every
 * picture tag is a segment, and the text is cut into sentences. Each segment remembers what stood in front
 * of it (sep): nothing (the first), a space (same line), a line break, or an empty line (new paragraph), so
 * the rules can be put together again exactly.
 *
 * A translation keeps a list of segments: per sentence the original (src), the translation (text) and its
 * status. When the original changes, sentences that stayed the same keep their translation and status;
 * only new and changed sentences are translated again (see planSegments).
 */

// Where a sentence ends: after . ! ? … (and their Chinese forms), followed by a space.
const SENTENCE_END = /(?<=[.!?…。！？])\s+(?=\S)/u;

// Segment statuses. A person has looked at approved, edited and review segments.
const STATUS = {
  auto: "auto",         // translated by the cloud, not checked by a person yet
  approved: "approved", // a person said the translation is right
  edited: "edited",     // a person corrected it (counts as checked)
  review: "review",     // a person had checked it, but the original sentence changed since: look again
  added: "added"        // a person added it in a translation: still to be put into the original
};
const CHECKED_BY_A_PERSON = new Set([STATUS.approved, STATUS.edited]);
const HUMAN = new Set([STATUS.approved, STATUS.edited, STATUS.review]);

/** Whether a segment is a picture tag. */
function isPictureSegment(segment) {
  return new RegExp(`^${IMAGE_TAG.source}$`).test(segment);
}

/**
 * The rules cut into segments: [{ src, sep, picture }]. Pictures stand alone; text is cut into paragraphs
 * (empty lines), lines and sentences.
 */
function segmentize(rulesText) {
  const text = String(rulesText || "");
  const segments = [];
  const addText = (piece) => {
    for (const paragraph of piece.split(/\r?\n[ \t]*\r?\n/)) {
      if (paragraph.trim().length === 0) continue;
      paragraph.trim().split(/\r?\n/).forEach((line, lineIndex) => {
        line.trim().split(SENTENCE_END).filter((sentence) => sentence.trim().length > 0).forEach((sentence, index) => {
          segments.push({ src: sentence.trim(), sep: index > 0 ? " " : lineIndex > 0 ? "\n" : "\n\n", picture: false });
        });
      });
    }
  };
  let position = 0;
  for (const match of text.matchAll(IMAGE_TAG)) {
    addText(text.slice(position, match.index));
    segments.push({ src: match[0], sep: "\n\n", picture: true });
    position = match.index + match[0].length;
  }
  addText(text.slice(position));
  if (segments.length > 0) segments[0] = { ...segments[0], sep: "" };
  return segments;
}

/**
 * Segments together again as rules text, with the text given per segment (the translation or the original).
 * Emptied segments (text removed by a person) are left out, with their separator. Must match Sentences.Join
 * in the app.
 */
function joinSegments(segments, textOf = (segment) => segment.src) {
  let result = "";
  for (const segment of segments) {
    const text = textOf(segment);
    if (typeof text !== "string" || text.trim().length === 0) continue;
    if (result.length > 0) result += segment.sep || " ";
    result += text;
  }
  return result;
}

/**
 * Which segment of the old list each segment of the new list is (an exact match, in order), or -1 for a
 * segment that is new or changed: the longest common subsequence, as diff tools find it.
 */
function matchSegments(oldSegments, newSegments) {
  const rows = oldSegments.length, columns = newSegments.length;
  const longest = Array.from({ length: rows + 1 }, () => new Array(columns + 1).fill(0));
  for (let i = rows - 1; i >= 0; i--)
    for (let j = columns - 1; j >= 0; j--)
      longest[i][j] = oldSegments[i] === newSegments[j] ? longest[i + 1][j + 1] + 1 : Math.max(longest[i + 1][j], longest[i][j + 1]);
  const match = new Array(columns).fill(-1);
  let i = 0, j = 0;
  while (i < rows && j < columns) {
    if (oldSegments[i] === newSegments[j]) { match[j] = i; i++; j++; }
    else if (longest[i + 1][j] >= longest[i][j + 1]) i++;
    else j++;
  }
  return match;
}

/**
 * A translation's segments for a (changed) original. Each result segment is { src, sep, picture, text,
 * status, suggestion }:
 * - the same sentence as before: its translation and status stay (also a person's correction);
 * - a changed sentence (in the same place as one that is gone) that a person had checked: their translation
 *   stays, status "review", and suggestion = null asks for a fresh automatic translation to compare with;
 * - any other new or changed sentence: text = null (to translate), status "auto".
 * Pictures keep their tag. Without old segments (a new game, or an older kind of translation) everything is
 * to be translated.
 */
function planSegments(oldSegments, source) {
  const fresh = (segment) => ({ ...segment, text: segment.picture ? segment.src : null, status: STATUS.auto, suggestion: undefined });
  if (!Array.isArray(oldSegments) || oldSegments.length === 0) return source.map(fresh);

  const match = matchSegments(oldSegments.map((o) => o.src), source.map((s) => s.src));
  // Pairs a changed sentence with the one it replaced: in the same gap between matches, in order.
  const replaced = new Array(source.length).fill(-1);
  let oldStart = 0, newStart = 0;
  for (let j = 0; j <= source.length; j++) {
    if (j < source.length && match[j] < 0) continue;
    const oldEnd = j < source.length ? match[j] : oldSegments.length;
    const newGap = [], oldGap = [];
    for (let k = newStart; k < j; k++) if (!source[k].picture) newGap.push(k);
    for (let k = oldStart; k < oldEnd; k++) if (!isPictureSegment(oldSegments[k].src)) oldGap.push(k);
    newGap.forEach((k, index) => { if (index < oldGap.length) replaced[k] = oldGap[index]; });
    oldStart = oldEnd + 1;
    newStart = j + 1;
  }

  return source.map((segment, j) => {
    if (segment.picture) return fresh(segment);
    if (match[j] >= 0) {
      const old = oldSegments[match[j]];
      return { ...segment, text: old.text, status: old.status || STATUS.auto, suggestion: old.suggestion, origin: old.origin };
    }
    const before = replaced[j] >= 0 ? oldSegments[replaced[j]] : null;
    if (before && HUMAN.has(before.status))
      return { ...segment, text: before.text, status: STATUS.review, suggestion: null };
    return fresh(segment);
  });
}

/** Every text segment has been checked by a person (approved or corrected) for this version of the original. */
function allChecked(segments) {
  return segments.every((segment) => segment.picture || CHECKED_BY_A_PERSON.has(segment.status));
}

/** The segments as they're stored in a translation: without the picture flag, and without empty suggestions. */
function storedSegments(segments) {
  return segments.map(({ src, sep, text, status, suggestion, origin }) => {
    const stored = { src, sep, text, status };
    if (suggestion) stored.suggestion = suggestion;
    if (origin) stored.origin = origin;
    return stored;
  });
}

module.exports = {
  LANGUAGES, IMAGE_TAG, sourceHash, appLanguageOf, keepsPictureText, isRudeHand,
  STATUS, isPictureSegment, segmentize, joinSegments, matchSegments, planSegments, allChecked, storedSegments
};
