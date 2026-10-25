using HighScores;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Popups
{
    /// <summary>
    /// Game over / victory screen with optional high score entry.
    /// From game code: endGamePopup.Show(won: true, score: 1234);
    /// Buttons: Retry -> RestartScene, Main Menu -> LoadMainMenu, Submit -> SubmitScore.
    /// </summary>
    public class EndGamePopup : Popup
    {
        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private string winTitle = "You Win!";
        [SerializeField] private string loseTitle = "Game Over";
        [SerializeField] private string scoreFormat = "Score: {0}";

        [Header("High scores (all optional)")]
        [SerializeField] private bool useHighScores = true;
        [SerializeField] private GameObject nameEntryRoot;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private HighScoreTable highScoreTable;

        private int _score;
        private bool _submitted;

        public void Show(bool won, int score)
        {
            Show(won ? winTitle : loseTitle, score);
        }

        public void Show(string title, int score)
        {
            _score = score;
            _submitted = false;

            if (titleText) titleText.text = title;
            if (scoreText) scoreText.text = string.Format(scoreFormat, score);

            bool canSubmit = useHighScores && HighScoreManager.IsHighScore(score);
            if (nameEntryRoot) nameEntryRoot.SetActive(canSubmit);
            if (submitButton) submitButton.interactable = canSubmit;
            if (nameInput)
            {
                nameInput.characterLimit = HighScoreManager.MaxNameLength;
                nameInput.text = HighScoreManager.LastPlayerName;
            }

            if (highScoreTable) highScoreTable.gameObject.SetActive(useHighScores);

            Open();
            if (highScoreTable && useHighScores) highScoreTable.Refresh();
        }

        public void SubmitScore()
        {
            if (_submitted || !useHighScores) return;
            _submitted = true;

            int rank = HighScoreManager.SaveScore(nameInput ? nameInput.text : string.Empty, _score);

            if (nameEntryRoot) nameEntryRoot.SetActive(false);
            if (submitButton) submitButton.interactable = false;
            if (highScoreTable) highScoreTable.Refresh(rank);
        }
    }
}
