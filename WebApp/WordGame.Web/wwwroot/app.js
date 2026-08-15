const elements = {
  statusDot: document.querySelector("#statusDot"),
  statusLabel: document.querySelector("#statusLabel"),
  statusMessage: document.querySelector("#statusMessage"),
  retryButton: document.querySelector("#retryButton"),
  vocabularyCount: document.querySelector("#vocabularyCount"),
  typeList: document.querySelector("#typeList"),
  databasePath: document.querySelector("#databasePath")
};

const numberFormatter = new Intl.NumberFormat("zh-TW");

async function loadDatabaseSummary() {
  showLoadingState();

  try {
    const [health, summary] = await Promise.all([
      fetchJson("/api/health"),
      fetchJson("/api/vocabulary/summary")
    ]);

    showConnectedState(health, summary);
  } catch (error) {
    showErrorState(error);
  }
}

async function fetchJson(url) {
  const response = await fetch(url, { headers: { Accept: "application/json" } });
  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new Error(payload?.message ?? payload?.detail ?? `HTTP ${response.status}`);
  }

  return response.json();
}

function showLoadingState() {
  elements.statusDot.className = "status-dot status-dot--loading";
  elements.statusLabel.textContent = "正在連接資料庫";
  elements.statusMessage.textContent = "以唯讀模式檢查 WordGame.db……";
  elements.retryButton.hidden = true;
}

function showConnectedState(health, summary) {
  elements.statusDot.className = "status-dot status-dot--ok";
  elements.statusLabel.textContent = "資料庫連線正常";
  elements.statusMessage.textContent = "第一階段唯讀檢查已完成，可以開始建立練習流程。";
  elements.vocabularyCount.textContent = numberFormatter.format(summary.count);
  elements.databasePath.textContent = health.databasePath;
  renderTypes(summary.types);
}

function showErrorState(error) {
  elements.statusDot.className = "status-dot status-dot--error";
  elements.statusLabel.textContent = "無法連接資料庫";
  elements.statusMessage.textContent = error.message;
  elements.vocabularyCount.textContent = "—";
  elements.databasePath.textContent = "請確認專案根目錄的 WordGame.db。";
  elements.typeList.replaceChildren(createTypeChip("尚未取得資料"));
  elements.retryButton.hidden = false;
}

function renderTypes(types) {
  const chips = types.length > 0
    ? types.map(createTypeChip)
    : [createTypeChip("沒有已分類資料")];
  elements.typeList.replaceChildren(...chips);
}

function createTypeChip(type) {
  const chip = document.createElement("span");
  chip.className = "type-chip";
  chip.textContent = type;
  return chip;
}

elements.retryButton.addEventListener("click", loadDatabaseSummary);
loadDatabaseSummary();
