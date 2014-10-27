using System.Windows.Forms;
using System.Drawing;
using System;

namespace SerenityFlow
{
    public static class Dialogs
    {
        public static void Alert(string message)
        {
            MessageBox.Show(message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        }

        public static bool ConfirmNo(string message)
        {
            return MessageBox.Show(message, "Onay", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        public static bool Confirm(string message)
        {
            return MessageBox.Show(message, "Onay", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes;
        }

        public static DialogResult ConfirmYesNoCancel(string message)
        {
            return MessageBox.Show(message, "Onay", MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
        }

        public static void Info(string message)
        {
            MessageBox.Show(message, "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Error(string message)
        {
            MessageBox.Show(message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void Error(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void Warning(string message)
        {
            MessageBox.Show(message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static bool ConfirmWarning(string message)
        {
            return MessageBox.Show(message, "Uyarı", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        public static DialogResult Prompt(string prompt, string title, ref string value)
        {
            return Prompt(prompt, title, value, 0, 0, ref value);
        }

        public static DialogResult Prompt(string prompt, string title, string defaultValue, ref string value)
        {
            return Prompt(prompt, title, defaultValue, 0, 0, ref value);
        }

        public static DialogResult Prompt(string prompt, string title, string defaultValue,
            int xPos, int yPos, ref string value)
        {
            var form = new PromptForm();
            form.Text = title;
            form.Prompt = prompt;
            form.Value = defaultValue;

            if ((xPos > 0) || (yPos > 0))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Left = xPos;
                form.Top = yPos;
            }

            DialogResult dialogResult = form.ShowDialog();
            if (dialogResult == DialogResult.OK)
                value = form.Value;

            return dialogResult;
        }

        public class PromptForm : Form
        {
            private Label lbPrompt = new Label();
            private TextBox edInput = new TextBox();
            private Button btOK = new Button();
            private Button btCancel = new Button();

            public PromptForm()
            {
                this.Height = 180;
                this.Width = 400;
                this.Controls.Add(lbPrompt);
                this.Controls.Add(edInput);
                this.Controls.Add(btOK);
                this.Controls.Add(btCancel);

                lbPrompt.Top = 10;
                lbPrompt.Left = 10;
                lbPrompt.Width = 370;
                lbPrompt.Height = 70;

                edInput.Left = lbPrompt.Left;
                edInput.Top = lbPrompt.Top + lbPrompt.Height + 6;
                edInput.Width = lbPrompt.Width;

                btOK.Text = "Tamam";
                btOK.DialogResult = System.Windows.Forms.DialogResult.OK;
                btOK.Top = edInput.Top + edInput.Height + 6;
                btOK.Left = 220;

                btCancel.Text = "İptal";
                btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                btCancel.Top = btOK.Top;
                btCancel.Left = 300;
                
                this.AcceptButton = btOK;
                this.CancelButton = btCancel;
            }

            public string Prompt
            {
                get { return lbPrompt.Text; }
                set { lbPrompt.Text = value; }
            }

            public string Value
            {
                get { return edInput.Text; }
                set { edInput.Text = value; }
            }
        }
    }
}
