// NextStepWeb - Minimal Vanilla JavaScript
// Strictly adheres to: no external JS frameworks, accessible ARIA, local preservation, two-tab sync

document.addEventListener('DOMContentLoaded', () => {
  initSituationInputForm();
  initSituationDetailsView();
  initTwoTabSync();
});

/**
 * Handles Situation Input Screen, Local Draft Persistence, and Loading States
 * Addresses Requirement 6 (1s, 5s, 15s meaningful progress) & Requirement 10 (Refresh / Back recovery)
 */
function initSituationInputForm() {
  const form = document.getElementById('situationForm');
  const textarea = document.getElementById('SituationText');
  const submitBtn = document.getElementById('analyzeSubmitBtn');
  const loadingContainer = document.getElementById('loadingExperience');
  const loadingMessage = document.getElementById('loadingMessageText');
  const loadingTimer = document.getElementById('loadingTimerSeconds');
  const draftBanner = document.getElementById('draftRestoredBanner');

  if (!form || !textarea) return;

  const DRAFT_KEY = 'nextstep_situation_draft';
  const PENDING_KEY = 'nextstep_pending_submit';

  // Restore draft if textarea is empty or upon browser refresh/back navigation
  const savedDraft = localStorage.getItem(DRAFT_KEY);
  const hadPendingSubmit = sessionStorage.getItem(PENDING_KEY);

  if (!textarea.value.trim() && savedDraft) {
    textarea.value = savedDraft;
  }

  // If a submission was in flight when the user refreshed or navigated back
  if (hadPendingSubmit && savedDraft) {
    sessionStorage.removeItem(PENDING_KEY);
    if (draftBanner) {
      draftBanner.style.display = 'block';
    }
    announceToScreenReader('Your entered situation was preserved. You can review and continue.');
  }

  // Continuously preserve text on every keystroke
  textarea.addEventListener('input', () => {
    localStorage.setItem(DRAFT_KEY, textarea.value);
  });

  // Scenario buttons auto-populate textarea and draft
  const scenarioButtons = document.querySelectorAll('.scenario-btn');
  scenarioButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const text = btn.getAttribute('data-text');
      if (text) {
        textarea.value = text;
        localStorage.setItem(DRAFT_KEY, text);
        textarea.focus();
        announceToScreenReader('Scenario text loaded into description.');
      }
    });
  });

  // Ensure retry button generates fresh ClientRequestId for retry submissions
  const retryBtn = document.getElementById('retryAnalysisBtn');
  if (retryBtn) {
    retryBtn.addEventListener('click', () => {
      const clientRequestIdInput = document.getElementById('ClientRequestId');
      if (clientRequestIdInput) {
        clientRequestIdInput.value = 'retry_' + Date.now() + '_' + Math.random().toString(36).substring(2, 10);
      }
    });
  }

  // Handle Form Submit and Progressive Loading Experience
  form.addEventListener('submit', (e) => {
    if (!textarea.value.trim()) {
      return; // let native validation catch it
    }

    // Preserve situation in localStorage during in-flight request
    // DO NOT remove draft here - ensures refresh/back during 15s AI wait retains user text!
    localStorage.setItem(DRAFT_KEY, textarea.value);
    sessionStorage.setItem(PENDING_KEY, 'true');

    // Disable button to prevent double-submit
    if (submitBtn) {
      submitBtn.disabled = true;
      submitBtn.setAttribute('aria-busy', 'true');
    }

    // Show loading state with progressive disclosure
    if (loadingContainer) {
      loadingContainer.classList.add('active');
      loadingContainer.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    announceToScreenReader('Situation submitted. Reviewing details...');

    let seconds = 0;
    const timerInterval = setInterval(() => {
      seconds++;
      if (loadingTimer) {
        loadingTimer.textContent = `${seconds}s elapsed`;
      }

      // Requirement 6: Meaningful state changes at approximately 1s, 5s, 15s without fake internal AI reasoning
      if (seconds === 1) {
        updateLoadingText("Situation received. Reviewing details...");
      } else if (seconds === 5) {
        updateLoadingText("Finding what matters...");
      } else if (seconds === 10) {
        updateLoadingText("Structuring priorities and recommended actions...");
      } else if (seconds === 15) {
        updateLoadingText("Taking longer than usual. Your situation is safely stored; finalizing response...");
        announceToScreenReader("Taking longer than usual. Your situation is safely stored; finalizing response.");
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
 * Handles Situation Details View, Cleans Drafts, and Manages Accessibility
 */
function initSituationDetailsView() {
  const situationMeta = document.getElementById('situationMetadata');
  if (!situationMeta) return;

  // Once details view is loaded successfully, clear input draft
  const DRAFT_KEY = 'nextstep_situation_draft';
  const PENDING_KEY = 'nextstep_pending_submit';
  localStorage.removeItem(DRAFT_KEY);
  sessionStorage.removeItem(PENDING_KEY);

  // Announce assessment readiness for screen readers
  announceToScreenReader('Situation assessment ready.');
}

/**
 * Two-Tab Conflict Detection (Lightweight Polling + Visibility Change)
 * Addresses Requirement 11
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

  // Poll every 5 seconds
  const interval = setInterval(checkStaleStatus, 5000);

  // Also check immediately when tab gains focus
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      checkStaleStatus();
    }
  });
}

/**
 * Screen Reader Live Region Announcement (Requirement 8)
 * Clean single announcement without token-level noise or duplicate regions
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
