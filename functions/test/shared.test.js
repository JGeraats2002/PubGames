// Tests for shared.js. Run in the functions folder with: npm test
const { test } = require("node:test");
const assert = require("node:assert/strict");
const path = require("path");
const { sourceHash, appLanguageOf, keepsPictureText, isRudeHand } = require("../shared");
const cases = require(path.join(__dirname, "..", "..", "tests", "shared-cases.json"));

test("sourceHash gives the same fingerprint as the app", () => {
  for (const example of cases.sourceHash)
    assert.equal(sourceHash(example.name, example.rulesText, example.cover), example.hash, example.name);
});

test("isRudeHand agrees with the app", () => {
  for (const example of cases.rudeHand)
    assert.equal(isRudeHand({ Kind: example.kind, Neutral: example.neutral, Fingers: example.fingers }), example.rude, example.about);
});

test("isRudeHand is false for items without fingers", () => {
  assert.equal(isRudeHand({ Kind: 4, Neutral: false }), false);
});

test("appLanguageOf turns Cloud Translation codes into the app's", () => {
  assert.equal(appLanguageOf("pt-BR"), "pt");
  assert.equal(appLanguageOf("zh-CN"), "zh");
  assert.equal(appLanguageOf("NL"), "nl");
  assert.equal(appLanguageOf("sv"), null);
  assert.equal(appLanguageOf(undefined), null);
});

test("keepsPictureText keeps names, numbers and empty texts", () => {
  assert.equal(keepsPictureText("TOEPEN", "Toepen"), true);
  assert.equal(keepsPictureText(" 10 ", "Toepen"), true);
  assert.equal(keepsPictureText("♠ 3", "Toepen"), true);
  assert.equal(keepsPictureText("", "Toepen"), true);
  assert.equal(keepsPictureText("½ + ¼", "Toepen"), true);
  assert.equal(keepsPictureText("Hoog", "Toepen"), false);
});

const { STATUS, segmentize, joinSegments, matchSegments, planSegments, allChecked, storedSegments } = require("../shared");

const TOEPEN = "Toepen is a card game. You get 4 cards.\nThe highest wins!\n\n![image](fsimg:abc)\n\nDone.";

test("segmentize cuts rules into sentences and pictures, and joinSegments puts them back exactly", () => {
  const segments = segmentize(TOEPEN);
  assert.deepEqual(segments.map((s) => s.src), ["Toepen is a card game.", "You get 4 cards.", "The highest wins!", "![image](fsimg:abc)", "Done."]);
  assert.deepEqual(segments.map((s) => s.sep), ["", " ", "\n", "\n\n", "\n\n"]);
  assert.equal(segments[3].picture, true);
  assert.equal(joinSegments(segments), TOEPEN);
  assert.deepEqual(segmentize(""), []);
  assert.deepEqual(segmentize("Eén.\r\n\r\nTwee?").map((s) => s.src), ["Eén.", "Twee?"]);
  assert.deepEqual(segmentize("第一句。第二句！").map((s) => s.src), ["第一句。第二句！"], "no space after Chinese punctuation: one segment");
});

test("matchSegments finds the sentences that stayed the same", () => {
  assert.deepEqual(matchSegments(["A", "B", "C"], ["A", "X", "B", "C2"]), [0, -1, 1, -1]);
});

/** A translation of TOEPEN with one sentence corrected by a person. */
function translatedToepen() {
  return planSegments(null, segmentize(TOEPEN)).map((s, i) =>
    ({ ...s, text: s.picture ? s.src : `T${i}`, status: i === 1 ? STATUS.edited : STATUS.auto }));
}

test("a new game: every sentence is to be translated", () => {
  const plan = planSegments(null, segmentize(TOEPEN));
  assert.deepEqual(plan.map((s) => s.text), [null, null, null, "![image](fsimg:abc)", null]);
  assert.ok(plan.every((s) => s.status === STATUS.auto));
});

test("an added sentence is the only one translated; the rest keeps translation and status", () => {
  const plan = planSegments(translatedToepen(), segmentize(TOEPEN.replace("The highest wins!", "The highest wins!\nA new rule.")));
  assert.deepEqual(plan.map((s) => [s.text, s.status]), [
    ["T0", "auto"], ["T1", "edited"], ["T2", "auto"], [null, "auto"], ["![image](fsimg:abc)", "auto"], ["T4", "auto"]]);
});

test("a changed sentence a person corrected keeps their text, to look at again, with a new suggestion", () => {
  const plan = planSegments(translatedToepen(), segmentize(TOEPEN.replace("You get 4 cards.", "You get 5 cards.")));
  assert.deepEqual([plan[1].text, plan[1].status, plan[1].suggestion], ["T1", STATUS.review, null]);
  assert.equal(plan[0].text, "T0");
});

test("a changed sentence nobody checked is simply translated again", () => {
  const plan = planSegments(translatedToepen(), segmentize(TOEPEN.replace("Done.", "Finished.")));
  assert.deepEqual([plan[4].text, plan[4].status], [null, STATUS.auto]);
});

test("removed sentences and pictures disappear from the translation", () => {
  const plan = planSegments(translatedToepen(), segmentize("Toepen is a card game. You get 4 cards.\nThe highest wins!\n\nDone."));
  assert.deepEqual(plan.map((s) => s.text), ["T0", "T1", "T2", "T4"]);
  assert.equal(joinSegments(plan, (s) => s.text), "T0 T1\nT2\n\nT4");
});

test("allChecked and storedSegments", () => {
  const plan = translatedToepen();
  assert.equal(allChecked(plan), false);
  assert.equal(allChecked(plan.map((s) => ({ ...s, status: STATUS.approved }))), true);
  const stored = storedSegments([{ src: "a", sep: "", picture: false, text: "b", status: "auto", suggestion: undefined }]);
  assert.deepEqual(stored, [{ src: "a", sep: "", text: "b", status: "auto" }]);
});

test("segmentize and joinSegments agree with the app (shared cases)", () => {
  for (const example of cases.segments) {
    const segments = segmentize(example.rules);
    assert.deepEqual(segments.map((s) => s.src), example.src);
    assert.deepEqual(segments.map((s) => s.sep), example.sep);
  }
  for (const example of cases.join) {
    const segments = example.texts.map((text, index) => ({ src: text, sep: example.sep[index], text }));
    assert.equal(joinSegments(segments, (s) => s.text), example.joined);
  }
});
