const elements = {
  connectionBadge: document.querySelector("#connectionBadge"),
  connectionText: document.querySelector("#connectionText"),
  vocabularyCount: document.querySelector("#vocabularyCount"),
  databaseName: document.querySelector("#databaseName"),
  setupView: document.querySelector("#setupView"),
  setupForm: document.querySelector("#setupForm"),
  minNumber: document.querySelector("#minNumber"),
  maxNumber: document.querySelector("#maxNumber"),
  vocabularyType: document.querySelector("#vocabularyType"),
  setupError: document.querySelector("#setupError"),
  startButton: document.querySelector("#startButton"),
  practiceView: document.querySelector("#practiceView"),
  endSessionButton: document.querySelector("#endSessionButton"),
  roundProgress: document.querySelector("#roundProgress"),
  roundNumber: document.querySelector("#roundNumber"),
  positionProgress: document.querySelector("#positionProgress"),
  questionPosition: document.querySelector("#questionPosition"),
  questionTotal: document.querySelector("#questionTotal"),
  reviewProgress: document.querySelector("#reviewProgress"),
  reviewCandidateCount: document.querySelector("#reviewCandidateCount"),
  directionLabel: document.querySelector("#directionLabel"),
  questionNumber: document.querySelector("#questionNumber"),
  questionPrompt: document.querySelector("#questionPrompt"),
  answerForm: document.querySelector("#answerForm"),
  answerInput: document.querySelector("#answerInput"),
  submitAnswerButton: document.querySelector("#submitAnswerButton"),
  toggleChoicesButton: document.querySelector("#toggleChoicesButton"),
  multipleChoicePanel: document.querySelector("#multipleChoicePanel"),
  multipleChoiceGrid: document.querySelector("#multipleChoiceGrid"),
  answerFeedback: document.querySelector("#answerFeedback"),
  revealPanel: document.querySelector("#revealPanel"),
  revealedAnswer: document.querySelector("#revealedAnswer"),
  translationLabel: document.querySelector("#translationLabel"),
  revealedTranslation: document.querySelector("#revealedTranslation"),
  exampleRow: document.querySelector("#exampleRow"),
  revealedExample: document.querySelector("#revealedExample"),
  resultProficiency: document.querySelector("#resultProficiency"),
  resultNextReview: document.querySelector("#resultNextReview"),
  revealButton: document.querySelector("#revealButton"),
  nextButton: document.querySelector("#nextButton")
};

const state = {
  sessionId: null,
  mode: "FullRandom",
  direction: "JpToCn",
  readyForNext: false,
  composingAnswer: false,
  requestInFlight: false,
  selectedChoiceButton: null
};

const numberFormatter = new Intl.NumberFormat("zh-TW");

async function initialize() {
  try {
    const [health, options] = await Promise.all([
      fetchJson("/api/health"),
      fetchJson("/api/practice/options")
    ]);
    showConnectedState(health);
    populatePracticeOptions(options);
  } catch (error) {
    showConnectionError(error);
  }
}

async function fetchJson(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    headers: {
      Accept: "application/json",
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...options.headers
    }
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new Error(payload?.message ?? payload?.detail ?? `HTTP ${response.status}`);
  }

  return response.status === 204 ? null : response.json();
}

function showConnectedState(health) {
  elements.connectionBadge.className = "connection-badge connection-badge--ok";
  elements.connectionText.textContent = "資料庫連線正常";
  elements.vocabularyCount.textContent = numberFormatter.format(health.vocabularyCount);
  elements.databaseName.textContent = getFileName(health.databasePath);
}

function showConnectionError(error) {
  elements.connectionBadge.className = "connection-badge connection-badge--error";
  elements.connectionText.textContent = "資料庫連線失敗";
  showSetupError(error.message);
}

function populatePracticeOptions(options) {
  elements.minNumber.value = options.minNumber;
  elements.maxNumber.value = options.maxNumber;
  elements.minNumber.min = options.minNumber;
  elements.minNumber.max = options.maxNumber;
  elements.maxNumber.min = options.minNumber;
  elements.maxNumber.max = options.maxNumber;

  const typeOptions = options.types.map(createTypeOption);
  elements.vocabularyType.append(...typeOptions);
  elements.startButton.disabled = options.maxNumber === 0;
}

function createTypeOption(type) {
  const option = document.createElement("option");
  option.value = type;
  option.textContent = type;
  return option;
}

function getFileName(path) {
  return path.split(/[\\/]/).pop() || "WordGame.db";
}

async function startPractice(event) {
  event.preventDefault();
  hideSetupError();

  const request = readPracticeSettings();
  if (!request)
    return;

  setButtonBusy(elements.startButton, true, "正在準備題目……");
  try {
    const response = await fetchJson("/api/practice/sessions", {
      method: "POST",
      body: JSON.stringify(request)
    });
    state.sessionId = response.sessionId;
    state.mode = request.mode;
    state.direction = request.direction;
    showPracticeView();
    renderQuestion(response.question);
  } catch (error) {
    showSetupError(error.message);
  } finally {
    setButtonBusy(elements.startButton, false);
  }
}

function readPracticeSettings() {
  const minNumber = Number(elements.minNumber.value);
  const maxNumber = Number(elements.maxNumber.value);
  if (!Number.isInteger(minNumber) || !Number.isInteger(maxNumber)) {
    showSetupError("請輸入有效的番号範圍。");
    return null;
  }
  if (minNumber > maxNumber) {
    showSetupError("起始番号不能大於結束番号。");
    return null;
  }

  return {
    minNumber,
    maxNumber,
    type: elements.vocabularyType.value || null,
    mode: new FormData(elements.setupForm).get("mode"),
    direction: new FormData(elements.setupForm).get("direction")
  };
}

function showPracticeView() {
  elements.setupView.hidden = true;
  elements.practiceView.hidden = false;
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function renderQuestion(question) {
  state.readyForNext = false;
  state.mode = question.mode;
  renderQuestionProgress(question);
  elements.questionNumber.textContent = question.number;
  elements.questionPrompt.textContent = question.prompt;
  elements.directionLabel.textContent = state.direction === "JpToCn"
    ? "日文 → 讀音"
    : "中文 → 日文";
  elements.translationLabel.textContent = state.direction === "JpToCn"
    ? "中文"
    : "讀音";

  elements.answerInput.value = "";
  elements.answerInput.disabled = false;
  elements.submitAnswerButton.disabled = false;
  elements.answerFeedback.textContent = "";
  elements.answerFeedback.className = "answer-feedback";
  elements.revealPanel.hidden = true;
  elements.revealButton.hidden = false;
  elements.revealButton.disabled = false;
  elements.toggleChoicesButton.hidden = false;
  elements.nextButton.hidden = true;
  elements.nextButton.disabled = false;
  renderMultipleChoices(question.choices);
  setMultipleChoiceVisibility(false, false);
  elements.answerInput.focus();
}

function renderMultipleChoices(choices) {
  const choiceButtons = choices.map(createMultipleChoiceButton);
  elements.multipleChoiceGrid.replaceChildren(...choiceButtons);
  elements.toggleChoicesButton.disabled = choiceButtons.length === 0;
}

function createMultipleChoiceButton(answer, index) {
  const button = document.createElement("button");
  const number = document.createElement("span");
  const text = document.createElement("span");

  button.type = "button";
  button.className = "answer-choice";
  button.dataset.answer = answer;
  button.dataset.rejected = "false";
  button.setAttribute("aria-label", `${index + 1}：${answer}`);
  number.className = "answer-choice__number";
  number.setAttribute("aria-hidden", "true");
  number.textContent = index + 1;
  text.className = "answer-choice__text";
  text.textContent = answer;
  button.append(number, text);
  button.addEventListener("click", () => submitMultipleChoice(button));
  return button;
}

function submitMultipleChoice(button) {
  if (button.disabled || state.readyForNext || state.requestInFlight)
    return;

  state.selectedChoiceButton = button;
  elements.answerInput.value = button.dataset.answer;
  elements.answerForm.requestSubmit();
}

function toggleMultipleChoices() {
  if (state.readyForNext || state.requestInFlight)
    return;

  setMultipleChoiceVisibility(elements.multipleChoicePanel.hidden, true);
}

function setMultipleChoiceVisibility(visible, shouldFocus = true) {
  const canShow = visible && elements.multipleChoiceGrid.children.length > 0;
  elements.multipleChoicePanel.hidden = !canShow;
  elements.toggleChoicesButton.setAttribute("aria-expanded", String(canShow));
  elements.toggleChoicesButton.textContent = canShow
    ? "隱藏四選一（C）"
    : "顯示四選一（C）";

  if (!shouldFocus)
    return;
  if (canShow)
    focusFirstAvailableChoice();
  else
    elements.answerInput.focus();
}

function focusFirstAvailableChoice() {
  getAvailableChoiceButtons()[0]?.focus();
}

function getChoiceButtons() {
  return [...elements.multipleChoiceGrid.querySelectorAll(".answer-choice")];
}

function getAvailableChoiceButtons() {
  return getChoiceButtons().filter(button => !button.disabled);
}

async function submitAnswer(event) {
  event.preventDefault();
  if (state.composingAnswer || state.readyForNext || state.requestInFlight)
    return;

  const answer = readSubmittedAnswer();
  if (!answer || !beginRequest())
    return;

  const selectedChoiceButton = state.selectedChoiceButton;
  state.selectedChoiceButton = null;
  setAnswerControlsDisabled(true);
  try {
    const result = await fetchJson(sessionUrl("answer"), {
      method: "POST",
      body: JSON.stringify({ answer })
    });
    if (result.isCorrect) {
      showReveal(result);
      return;
    }

    showIncorrectAnswer(selectedChoiceButton);
  } catch (error) {
    showAnswerFeedback(error.message, "wrong");
  } finally {
    finishRequest();
    if (!state.readyForNext)
      setAnswerControlsDisabled(false);
    if (selectedChoiceButton && !state.readyForNext)
      focusFirstAvailableChoice();
  }
}

function readSubmittedAnswer() {
  const answer = elements.answerInput.value.trim();
  if (answer)
    return answer;

  showAnswerFeedback("請先輸入答案，或選擇「不知道」。", "wrong");
  elements.answerInput.focus();
  return null;
}

function showIncorrectAnswer(selectedChoiceButton) {
  showAnswerFeedback("不對，再想一下。", "wrong");
  if (selectedChoiceButton)
    rejectChoice(selectedChoiceButton);
  else
    elements.answerInput.select();
}

async function revealAnswer() {
  if (!beginRequest())
    return;

  setAnswerControlsDisabled(true);
  try {
    const result = await fetchJson(sessionUrl("reveal"), { method: "POST" });
    showReveal(result);
  } catch (error) {
    showAnswerFeedback(error.message, "wrong");
    setAnswerControlsDisabled(false);
  } finally {
    finishRequest();
  }
}

function rejectChoice(button) {
  button.dataset.rejected = "true";
  button.classList.add("answer-choice--rejected");
  button.disabled = true;
  button.setAttribute("aria-label", `${button.getAttribute("aria-label")}，已排除`);
}

function showReveal(result) {
  state.readyForNext = true;
  showRecordedOutcome(result);
  const reveal = result.reveal;
  elements.revealedAnswer.textContent = reveal.answer;
  elements.revealedTranslation.textContent = reveal.translation;
  elements.revealedExample.textContent = reveal.example;
  elements.exampleRow.hidden = !reveal.example;
  elements.resultProficiency.textContent = result.progress.proficiency;
  elements.resultNextReview.textContent = formatNextReview(result.progress.nextReview);
  elements.revealPanel.hidden = false;
  elements.answerInput.disabled = true;
  elements.submitAnswerButton.disabled = true;
  elements.revealButton.hidden = true;
  elements.toggleChoicesButton.hidden = true;
  setMultipleChoiceVisibility(false, false);
  elements.nextButton.hidden = false;
  elements.nextButton.focus();
}

function renderQuestionProgress(question) {
  const isFullRandom = question.mode === "FullRandom";
  elements.roundProgress.hidden = !isFullRandom;
  elements.positionProgress.hidden = !isFullRandom;
  elements.reviewProgress.hidden = isFullRandom;

  if (isFullRandom) {
    elements.roundNumber.textContent = question.round;
    elements.questionPosition.textContent = question.position;
    elements.questionTotal.textContent = question.total;
    return;
  }

  elements.reviewCandidateCount.textContent = numberFormatter.format(question.total);
}

function showRecordedOutcome(result) {
  if (result.recordedOutcome === "Correct") {
    showAnswerFeedback("答對了，已記錄為正確。", "correct");
    return;
  }

  if (result.isCorrect) {
    showAnswerFeedback("這次答對了；本題依第一次作答記錄為錯誤。", "partial");
    return;
  }

  showAnswerFeedback("已顯示答案，本題記錄為錯誤。", "wrong");
}

function formatNextReview(timestamp) {
  return new Intl.DateTimeFormat("zh-TW", {
    year: "numeric",
    month: "numeric",
    day: "numeric"
  }).format(new Date(timestamp));
}

function showAnswerFeedback(message, tone) {
  elements.answerFeedback.textContent = message;
  elements.answerFeedback.className = `answer-feedback answer-feedback--${tone}`;
}

async function loadNextQuestion() {
  if (!beginRequest())
    return;

  elements.nextButton.disabled = true;
  try {
    const question = await fetchJson(sessionUrl("next"), { method: "POST" });
    renderQuestion(question);
  } catch (error) {
    showAnswerFeedback(error.message, "wrong");
    elements.nextButton.disabled = false;
  } finally {
    finishRequest();
  }
}

async function endPractice() {
  if (!beginRequest())
    return;

  const sessionId = state.sessionId;
  elements.endSessionButton.disabled = true;
  try {
    if (sessionId) {
      await fetchJson(`/api/practice/sessions/${sessionId}`, {
        method: "DELETE"
      });
    }
    state.sessionId = null;
    state.readyForNext = false;
    elements.practiceView.hidden = true;
    elements.setupView.hidden = false;
    elements.startButton.focus();
  } catch (error) {
    showAnswerFeedback(error.message, "wrong");
  } finally {
    finishRequest();
    elements.endSessionButton.disabled = false;
  }
}

function sessionUrl(action) {
  return `/api/practice/sessions/${state.sessionId}/${action}`;
}

function setAnswerControlsDisabled(disabled) {
  elements.answerInput.disabled = disabled;
  elements.submitAnswerButton.disabled = disabled;
  elements.revealButton.disabled = disabled;
  elements.toggleChoicesButton.disabled = disabled;
  getChoiceButtons().forEach(button => {
    button.disabled = disabled || button.dataset.rejected === "true";
  });
}

function beginRequest() {
  if (state.requestInFlight)
    return false;
  state.requestInFlight = true;
  return true;
}

function finishRequest() {
  state.requestInFlight = false;
}

function setButtonBusy(button, busy, busyText = "處理中……") {
  if (busy) {
    button.dataset.originalText = button.textContent;
    button.textContent = busyText;
    button.disabled = true;
    return;
  }

  button.textContent = button.dataset.originalText || button.textContent;
  button.disabled = false;
}

function showSetupError(message) {
  elements.setupError.textContent = message;
  elements.setupError.hidden = false;
}

function hideSetupError() {
  elements.setupError.hidden = true;
  elements.setupError.textContent = "";
}

function updateStartButtonLabel() {
  const mode = new FormData(elements.setupForm).get("mode");
  elements.startButton.textContent = mode === "Proficiency"
    ? "開始熟練度複習"
    : "開始完全隨機練習";
}

function abandonSessionOnPageHide() {
  const sessionId = state.sessionId;
  if (!sessionId)
    return;

  state.sessionId = null;
  fetch(`/api/practice/sessions/${sessionId}`, {
    method: "DELETE",
    keepalive: true
  }).catch(() => null);
}

elements.setupForm.addEventListener("submit", startPractice);
elements.answerForm.addEventListener("submit", submitAnswer);
elements.answerInput.addEventListener("compositionstart", () => {
  state.composingAnswer = true;
});
elements.answerInput.addEventListener("compositionend", () => {
  state.composingAnswer = false;
});
elements.revealButton.addEventListener("click", revealAnswer);
elements.toggleChoicesButton.addEventListener("click", toggleMultipleChoices);
elements.nextButton.addEventListener("click", loadNextQuestion);
elements.endSessionButton.addEventListener("click", endPractice);
document.querySelectorAll('input[name="mode"]').forEach(input => {
  input.addEventListener("change", updateStartButtonLabel);
});
document.addEventListener("keydown", handleKeyboardShortcut);
window.addEventListener("pagehide", abandonSessionOnPageHide);

function handleKeyboardShortcut(event) {
  if (event.repeat || state.composingAnswer)
    return;

  if (event.key === "Enter") {
    handleEnterShortcut(event);
    return;
  }

  if (elements.practiceView.hidden || state.readyForNext || state.requestInFlight)
    return;

  if (tryHideMultipleChoices(event))
    return;

  if (isEditableElement(event.target))
    return;

  if (tryToggleMultipleChoices(event))
    return;

  if (elements.multipleChoicePanel.hidden)
    return;

  if (trySelectMultipleChoice(event))
    return;

  tryMoveChoiceFocus(event);
}

function tryHideMultipleChoices(event) {
  if (event.key !== "Escape" || elements.multipleChoicePanel.hidden)
    return false;

  event.preventDefault();
  setMultipleChoiceVisibility(false, true);
  return true;
}

function tryToggleMultipleChoices(event) {
  if (event.key.toLowerCase() !== "c")
    return false;

  event.preventDefault();
  toggleMultipleChoices();
  return true;
}

function trySelectMultipleChoice(event) {
  if (!/^[1-4]$/.test(event.key))
    return false;

  event.preventDefault();
  getChoiceButtons()[Number(event.key) - 1]?.click();
  return true;
}

function tryMoveChoiceFocus(event) {
  const arrowKeys = ["ArrowLeft", "ArrowUp", "ArrowRight", "ArrowDown"];
  if (!arrowKeys.includes(event.key))
    return false;

  event.preventDefault();
  moveChoiceFocus(event.key);
  return true;
}

function handleEnterShortcut(event) {
  if (state.requestInFlight)
    return;

  if (state.readyForNext) {
    event.preventDefault();
    if (!elements.nextButton.disabled)
      loadNextQuestion();
    return;
  }

  if (!elements.practiceView.hidden && document.activeElement === elements.answerInput) {
    event.preventDefault();
    elements.answerForm.requestSubmit();
  }
}

function isEditableElement(target) {
  return target instanceof HTMLInputElement
    || target instanceof HTMLTextAreaElement
    || target instanceof HTMLSelectElement;
}

function moveChoiceFocus(key) {
  const buttons = getAvailableChoiceButtons();
  if (buttons.length === 0)
    return;

  const currentIndex = buttons.indexOf(document.activeElement);
  const direction = key === "ArrowLeft" || key === "ArrowUp" ? -1 : 1;
  const nextIndex = currentIndex < 0
    ? 0
    : (currentIndex + direction + buttons.length) % buttons.length;
  buttons[nextIndex].focus();
}

initialize();
