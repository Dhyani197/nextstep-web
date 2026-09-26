// NextStepWeb - Minimal Vanilla JavaScript
// Strictly adheres to: no external JS frameworks, accessible ARIA, local preservation, two-tab sync

document.addEventListener('DOMContentLoaded', () => {
  initSituationInputForm();
  initTwoTabSync();
});

/**
 * Handles Situation Input Screen, Local Draft Persistence, and Loading States
 */
function initSituationInputForm() {
  const form = document.getElementById('situationForm');
  const textarea = document.getElementById('SituationText');
  const submitBtn = document.getElementById('analyzeSubmitBtn');
  const loadingContainer = document.getElementById('loadingExperience');
  const loadingMessage = document.getElementById('loadingMessageText');
  const loadingTimer = document.getElementById('loadingTimerSeconds');
  const liveRegion = document.getElementById('a11yLiveRegion');

  if (!form || !textarea) return;

  const DRAFT_KEY = 'nextstep_situation_draft';

  // Restore draft if textarea is empty and not pre-seeded with a scenario
  if (!textarea.value.trim()) {
    const savedDraft = localStorage.getItem(DRAFT_KEY);
    if (savedDraft) {
      textarea.value = savedDraft;
    }
  }

  // Preserve text on every input
  textarea.addEventListener('input', () => {
    localStorage.setItem(DRAFT_KEY, textarea.value);
  });

  // Scenario buttons auto-populate textarea and draft
  const scenarioButtons = document.querySelectorAll('.scenario-btn');
  scenarioButtons.forEach(btn => {
    btn.addEventListener('click', (e) => {
      const text = btn.getAttribute('data-text');
      if (text) {
        textarea.value = text;
        localStorage.setItem(DRAFT_KEY, text);
        textarea.focus();
        announceToScreenReader('Scenario text loaded into description.');
      }
    });
  });

  // Handle Form Submit and Meaningful Loading Experience
  form.addEventListener('submit', (e) => {
    if (!textarea.value.trim()) {
      return; // let native/HTML5 validation catch it
    }

    // Disable button to prevent double-submit
    if (submitBtn) {
      submitBtn.disabled = true;
      submitBtn.setAttribute('aria-busy', 'true');
    }

    // Clear draft storage now that it's submitted
    localStorage.removeItem(DRAFT_KEY);

    // Show loading state with progressive disclosure
    if (loadingContainer) {
      loadingContainer.classList.add('active');
      loadingContainer.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    announceToScreenReader('Understanding your situation. Please wait.');

    let seconds = 0;
    const timerInterval = setInterval(() => {
      seconds++;
      if (loadingTimer) {
        loadingTimer.textContent = `${seconds}s elapsed`;
      }

      if (seconds === 1) {
        updateLoadingText("Understanding your situation...");
      } else if (seconds === 5) {
        updateLoadingText("Still working through the details...");
      } else if (seconds === 15) {
        updateLoadingText("This is taking longer than expected. Your situation has been saved.");
        announceToScreenReader("This is taking longer than expected. Your situation has been saved.");
      }
    }, 1000);
  });

  function updateLoadingText(text) {
    if (loadingMessage) {
      loadingMessage.textContent = text;
    }
  }
}

/**
 * Two-Tab Conflict Detection (Lightweight Polling + Visibility Change)
 */
function initTwoTabSync() {
  const staleBanner = document.getElementById('twoTabStaleBanner');
  const situationMeta = document.getElementById('situationMetadata');
  if (!situationMeta || !staleBanner) return;

  const situationId = situationMeta.getAttribute('data-situation-id');
  const currentVersion = parseInt(situationMeta.getAttribute('data-version'), 10);

  if (!situationId || isNaN(currentVersion)) return;

  async function checkStaleStatus() {
    try {
      const response = await fetch(`/Situation/CheckStale?id=${situationId}&v=${currentVersion}`, {
        headers: { 'Accept': 'application/json' }
      });
      if (response.ok) {
        const data = await response.json();
        if (data.isStale) {
          staleBanner.classList.add('visible');
          announceToScreenReader('This situation was updated in another tab.');
        }
      }
    } catch {
      // Offline or network hiccup, ignore silently
    }
  }

  // Poll every 6 seconds
  const interval = setInterval(checkStaleStatus, 6000);

  // Also check immediately when tab gains focus
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      checkStaleStatus();
    }
  });
}

/**
 * Screen Reader Live Region Announcement
 */
function announceToScreenReader(message) {
  const liveRegion = document.getElementById('a11yLiveRegion');
  if (liveRegion) {
    liveRegion.textContent = '';
    setTimeout(() => {
      liveRegion.textContent = message;
    }, 50);
  }
}
