using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProcessSimulateSnippets
{
    public class RLDebugPanel : Form
    {
        private Label _lblEpisode;
        private Label _lblStep;
        private Label _lblGripper;
        private Label _lblActionZero;
        private Label _lblActionOne;
        private Label _lblActionFive;
        private Label _lblTotalTime;
        private Label _lblSlider1;
        private Label _lblSlider2;
        private Label _lblSlider3;
        private Label _lblLastAction;
        private Label _lblLastReward;

        public RLDebugPanel()
        {
            this.Text = "RL Debug Panel";
            this.Size = new Size(300, 420);
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(10, 10);

            int y = 10;
            int spacing = 28;

            _lblEpisode = AddLabel("Episode: 0", ref y, spacing);
            _lblStep = AddLabel("Step: 0", ref y, spacing);
            _lblLastAction = AddLabel("Last action: -", ref y, spacing);
            _lblLastReward = AddLabel("Last reward: -", ref y, spacing);
            _lblGripper = AddLabel("Gripper: -", ref y, spacing);
            _lblActionZero = AddLabel("Action 0 (Type A): NOT DONE", ref y, spacing);
            _lblActionOne = AddLabel("Action 1 (Type B): NOT DONE", ref y, spacing);
            _lblActionFive = AddLabel("Action 5 (Crates): NOT DONE", ref y, spacing);
            _lblTotalTime = AddLabel("Total time: 0.00s", ref y, spacing);

            // Separator
            y += 10;
            Label sep = new Label();
            sep.Text = "--- Slider Status ---";
            sep.Location = new Point(10, y);
            sep.AutoSize = true;
            sep.Font = new Font(this.Font, FontStyle.Bold);
            this.Controls.Add(sep);
            y += spacing;

            _lblSlider1 = AddLabel("Slot 1 (low):    EMPTY", ref y, spacing);
            _lblSlider2 = AddLabel("Slot 2 (middle): EMPTY", ref y, spacing);
            _lblSlider3 = AddLabel("Slot 3 (top):    EMPTY", ref y, spacing);
        }

        private Label AddLabel(string text, ref int y, int spacing)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Location = new Point(10, y);
            lbl.AutoSize = true;
            this.Controls.Add(lbl);
            y += spacing;
            return lbl;
        }

        public void UpdateState(int episode, int step, int lastAction, double lastReward,
            string gripper, bool actionZeroDone, bool actionOneDone, bool actionFiveDone,
            double totalTime, int slider1, int slider2, int slider3)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateState(episode, step, lastAction, lastReward,
                    gripper, actionZeroDone, actionOneDone, actionFiveDone, totalTime, slider1, slider2, slider3)));
                return;
            }

            _lblEpisode.Text = $"Episode: {episode}";
            _lblStep.Text = $"Step: {step}";
            _lblLastAction.Text = $"Last action: {lastAction}";
            _lblLastReward.Text = $"Last reward: {lastReward:F3}";
            _lblGripper.Text = $"Gripper: {gripper}";
            _lblActionZero.Text = $"Action 0 (Type A): {(actionZeroDone ? "DONE" : "NOT DONE")}";
            _lblActionOne.Text = $"Action 1 (Type B): {(actionOneDone ? "DONE" : "NOT DONE")}";
            _lblActionFive.Text = $"Action 5 (Crates): {(actionFiveDone ? "DONE" : "NOT DONE")}";
            _lblTotalTime.Text = $"Total time: {totalTime:F2}s";

            _lblSlider1.Text = $"Slot 1 (low):    {(slider1 == 1 ? "EMPTY" : "OCCUPIED")}";
            _lblSlider1.ForeColor = slider1 == 1 ? Color.Green : Color.Red;

            _lblSlider2.Text = $"Slot 2 (middle): {(slider2 == 1 ? "EMPTY" : "OCCUPIED")}";
            _lblSlider2.ForeColor = slider2 == 1 ? Color.Green : Color.Red;

            _lblSlider3.Text = $"Slot 3 (top):    {(slider3 == 1 ? "EMPTY" : "OCCUPIED")}";
            _lblSlider3.ForeColor = slider3 == 1 ? Color.Green : Color.Red;
        }
    }
}