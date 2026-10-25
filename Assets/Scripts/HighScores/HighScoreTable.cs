using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace HighScores
{
    /// <summary>
    /// Renders the local high score table into a single TMP text. Refreshes whenever enabled.
    /// </summary>
    public class HighScoreTable : MonoBehaviour
    {
        [SerializeField] private TMP_Text tableText;
        [SerializeField] private Color highlightColour = new(1f, 0.85f, 0.2f);
        [SerializeField] private string emptyMessage = "No scores yet - be the first!";

        private int _highlightRank;

        private void OnEnable()
        {
            Refresh();
        }

        /// <param name="highlightRank">1-based rank to highlight (e.g. the score just submitted), 0 for none.</param>
        public void Refresh(int highlightRank = 0)
        {
            _highlightRank = highlightRank;
            if (!tableText) return;

            List<HighScoreEntry> entries = HighScoreManager.GetTopScores();
            if (entries.Count == 0)
            {
                tableText.text = emptyMessage;
                return;
            }

            string highlightHex = ColorUtility.ToHtmlStringRGB(highlightColour);
            StringBuilder sb = new();
            for (int i = 0; i < entries.Count; i++)
            {
                int rank = i + 1;
                string line = $"{rank}.<pos=12%>{entries[i].playerName}<pos=75%>{entries[i].score}";
                sb.AppendLine(rank == _highlightRank ? $"<color=#{highlightHex}>{line}</color>" : line);
            }

            tableText.text = sb.ToString();
        }
    }
}
