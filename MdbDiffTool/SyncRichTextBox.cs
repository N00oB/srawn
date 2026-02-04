using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MdbDiffTool
{
    /// <summary>
    /// RichTextBox с синхронным вертикальным скроллом с напарником.
    /// - Колёсико мыши: свой скролл по целым строкам (EM_LINESCROLL) без base.WndProc.
    /// - Стрелки / PageUp/PageDown / перетаскивание ползунка:
    ///   синхронизация по первой видимой строке.
    /// - После окончания перетаскивания ползунка (SB_ENDSCROLL) выравниваем
    ///   верхнюю строку по целой строке без постоянного мерцания.
    /// </summary>
    public class SyncRichTextBox : RichTextBox
    {
        public SyncRichTextBox SyncPartner { get; set; }

        private bool _skipSync;

        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100;

        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;

        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_PRIOR = 0x21; // PageUp
        private const int VK_NEXT = 0x22; // PageDown

        // коды для wParam низкое слово в WM_VSCROLL
        private const int SB_ENDSCROLL = 8;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        protected override void WndProc(ref Message m)
        {
            if (SyncPartner == null || _skipSync)
            {
                base.WndProc(ref m);
                return;
            }

            // 1) Колёсико — полностью забираем на себя.
            if (m.Msg == WM_MOUSEWHEEL)
            {
                HandleMouseWheel(m);
                return; // base.WndProc НЕ вызываем
            }

            // 2) Клавиши / ползунок — синхронизация по первой видимой строке.
            bool needSync = false;
            bool alignAfter = false; // выравнивать ли по целой строке после base.WndProc

            if (m.Msg == WM_VSCROLL)
            {
                needSync = true;
                // низкое слово wParam — код события скролла
                int code = m.WParam.ToInt32() & 0xFFFF;
                if (code == SB_ENDSCROLL)
                    alignAfter = true; // закончили перетаскивание ползунка
            }
            else if (m.Msg == WM_KEYDOWN)
            {
                int vk = m.WParam.ToInt32();
                if (vk == VK_UP || vk == VK_DOWN ||
                    vk == VK_PRIOR || vk == VK_NEXT)
                {
                    needSync = true;
                }
            }

            if (!needSync)
            {
                base.WndProc(ref m);
                return;
            }

            int oldFirst = GetFirstVisibleLine(this);

            base.WndProc(ref m);

            if (alignAfter)
            {
                // один раз после отпускания ползунка выравниваем по целой строке
                AlignToWholeLine(this);
            }

            int newFirst = GetFirstVisibleLine(this);
            int delta = newFirst - oldFirst;
            if (delta == 0)
                return;

            try
            {
                _skipSync = true;
                // напарник — на ту же дельту строк (у него полстроки не бывает, потому
                // что мы двигаем его только EM_LINESCROLL-ом)
                SendMessage(SyncPartner.Handle, EM_LINESCROLL, 0, delta);
            }
            finally
            {
                _skipSync = false;
            }
        }

        /// <summary>
        /// Скролл по колесу: крутим оба RichTextBox-а на фиксированное число строк.
        /// </summary>
        private void HandleMouseWheel(Message m)
        {
            long wparam = m.WParam.ToInt64();
            short deltaRaw = (short)((wparam >> 16) & 0xFFFF);
            if (deltaRaw == 0)
                return;

            int linesPerClick = SystemInformation.MouseWheelScrollLines;
            if (linesPerClick <= 0)
                linesPerClick = 3;

            int sign = deltaRaw > 0 ? 1 : -1;
            int deltaLines = -sign * linesPerClick; // кручу вниз → текст вверх

            try
            {
                _skipSync = true;

                SendMessage(this.Handle, EM_LINESCROLL, 0, deltaLines);
                SendMessage(SyncPartner.Handle, EM_LINESCROLL, 0, deltaLines);
                // EM_LINESCROLL сам работает по строкам, доп. выравнивание не нужно
            }
            finally
            {
                _skipSync = false;
            }
        }

        private static int GetFirstVisibleLine(RichTextBox rtb)
        {
            if (rtb == null || rtb.IsDisposed)
                return 0;

            return SendMessage(rtb.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        }

        /// <summary>
        /// Выравнивание так, чтобы верхняя строка была целой (используем редко).
        /// </summary>
        private void AlignToWholeLine(RichTextBox rtb)
        {
            if (rtb == null || rtb.IsDisposed)
                return;

            int line = GetFirstVisibleLine(rtb);
            if (line < 0 || line >= rtb.Lines.Length)
                return;

            int charIndex = rtb.GetFirstCharIndexFromLine(line);
            if (charIndex < 0)
                return;

            bool oldSkip = _skipSync;
            _skipSync = true;
            try
            {
                rtb.SelectionStart = charIndex;
                rtb.SelectionLength = 0;
                rtb.ScrollToCaret();
            }
            finally
            {
                _skipSync = oldSkip;
            }
        }
    }
}
