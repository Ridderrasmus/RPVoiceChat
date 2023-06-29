using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace rpvoicechat.Client.Utils
{
    public class GlobalKeyboardHook
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public event EventHandler<KeyEventArgs> KeyDown;
        public event EventHandler<KeyEventArgs> KeyUp;

        private Form hookForm;

        public GlobalKeyboardHook()
        {
            hookForm = new Form();
            hookForm.KeyDown += (sender, e) => KeyDown?.Invoke(this, e);
            hookForm.KeyUp += (sender, e) => KeyUp?.Invoke(this, e);
        }

        public void Hook()
        {
            hookForm.Show();
            hookForm.Visible = false;
            hookForm.Focus();
        }

        public void Unhook()
        {
            hookForm.Close();
        }
    }
}
