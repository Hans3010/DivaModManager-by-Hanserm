using DivaModManager.Common.MessageWindow;
using DivaModManager.Features.Debug;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DivaModManager.Common.Helpers
{
    public static class WindowHelper
    {
        public enum WindowCloseStatus
        {
            None,
            Yes,
            No,
            Cancel,
            YesCheck,
            NoCheck,
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message_no"></param>
        /// <param name="replaceList"></param>
        /// <param name="obj"></param>
        /// <param name="path"></param>
        /// <param name="yesno"></param>
        /// <returns></returns>
        public static WindowCloseStatus DMMWindowOpen(int message_no, List<string> replaceList = null, object obj = null, string path = "", bool yesno = false)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"message_no:{message_no}, replaceList:{Util.GetListToParamString(replaceList)}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            WindowInfo info = WindowList.MessageWindowNo(message_no, replaceList);
            var ret = WindowCloseStatus.None;

            if (info.WindowType.ToString().ToUpper() == WindowInfo.MESSAGE_WINDOW.OK.ToString().ToUpper())
            {
                var msgOK = new DmmMessageWindowOK(info.Info(), info.Context(), $"[{info.WindowId()}] {info.WindowTitle()}", true);
                msgOK.MessageInfo.Text = info.Info();
                msgOK.MessageText.Text = info.Context();
                msgOK.Button_1.Content = info.Button_1();
                msgOK.Owner = App.Current.MainWindow.GetType() != msgOK.GetType() ? App.Current.MainWindow : null;
                msgOK.ShowDialog();
                msgOK.Activate();
                msgOK.Focus();
                if (msgOK.IsCancel == true)
                    ret = WindowCloseStatus.Cancel;
                else if (msgOK.OK)
                    ret = WindowCloseStatus.Yes;
                else
                    ret = WindowCloseStatus.No;
            }
            else if (info.WindowType.ToString().ToUpper() == WindowInfo.MESSAGE_WINDOW.OKCheck.ToString().ToUpper())
            {
                var msgOKCheck = new DmmMessageWindowOKCheck(info.Info(), info.Context(), $"[{info.WindowId()}] {info.WindowTitle()}", true);
                msgOKCheck.MessageInfo.Text = info.Info();
                msgOKCheck.MessageText.Text = info.Context();
                msgOKCheck.Button_1.Content = info.Button_1();
                msgOKCheck.Check_1.Content = info.Check_1();
                msgOKCheck.Owner = App.Current.MainWindow.GetType() != msgOKCheck.GetType() ? App.Current.MainWindow : null;
                msgOKCheck.ShowDialog();
                msgOKCheck.Activate();
                msgOKCheck.Focus();
                if (msgOKCheck.IsCancel == true)
                    ret = WindowCloseStatus.Cancel;
                else if (msgOKCheck.OK)
                    if (msgOKCheck.Checked)
                        ret = WindowCloseStatus.YesCheck;
                    else
                        ret = WindowCloseStatus.Yes;
                else
                    if (msgOKCheck.Checked)
                        ret = WindowCloseStatus.NoCheck;
                    else
                        ret = WindowCloseStatus.No;
            }
            else if (info.WindowType.ToString().ToUpper() == WindowInfo.MESSAGE_WINDOW.YesNo.ToString().ToUpper())
            {
                var msgYesNo = new DmmMessageWindowYesNo(info.Info(), info.Context(), $"[{info.WindowId()}] {info.WindowTitle()}");
                msgYesNo.MessageInfo.Text = info.Info();
                msgYesNo.MessageText.Text = info.Context();
                msgYesNo.Button_1.Content = info.Button_1();
                msgYesNo.Button_2.Content = info.Button_2();
                msgYesNo.Owner = App.Current.MainWindow.GetType() != msgYesNo.GetType() ? App.Current.MainWindow : null;
                msgYesNo.ShowDialog();
                msgYesNo.Activate();
                msgYesNo.Focus();
                if (msgYesNo.IsCancel == true)
                    ret = WindowCloseStatus.Cancel;
                else if (msgYesNo.YesNo)
                    ret = WindowCloseStatus.Yes;
                else
                    ret = WindowCloseStatus.No;
            }
            else if (info.WindowType.ToString().ToUpper() == WindowInfo.MESSAGE_WINDOW.YesNoCheck.ToString().ToUpper())
            {
                var msgYesNoCheck = new DmmMessageWindowYesNoCheck(info.Info(), info.Context(), $"[{info.WindowId()}] {info.WindowTitle()}");
                msgYesNoCheck.MessageInfo.Text = info.Info();
                msgYesNoCheck.MessageText.Text = info.Context();
                msgYesNoCheck.Button_1.Content = info.Button_1();
                msgYesNoCheck.Button_2.Content = info.Button_2();
                msgYesNoCheck.Check_1.Content = info.Check_1();
                msgYesNoCheck.ShowDialog();
                msgYesNoCheck.Activate();
                msgYesNoCheck.Focus();
                if (msgYesNoCheck.IsCancel == true)
                    ret = WindowCloseStatus.Cancel;
                else if (msgYesNoCheck.YesNo)
                    if (msgYesNoCheck.Checked)
                        ret = WindowCloseStatus.YesCheck;
                    else
                        ret = WindowCloseStatus.Yes;
                else
                    if (msgYesNoCheck.Checked)
                        ret = WindowCloseStatus.NoCheck;
                    else
                        ret = WindowCloseStatus.No;
            }
            else if (info.WindowType.ToString().ToUpper() == WindowInfo.MESSAGE_WINDOW.YesNoOpen.ToString().ToUpper())
            {
                var msgYesNoOpen = new DmmMessageWindowYesNoOpen(info.Info(), info.Context(), $"[{info.WindowId()}] {info.WindowTitle()}", path, yesno);
                msgYesNoOpen.Button_1.Content = info.Button_1();
                msgYesNoOpen.Button_2.Content = info.Button_2();
                msgYesNoOpen.Button_3.Content = info.Button_3();
                msgYesNoOpen.Owner = App.Current.MainWindow.GetType() != msgYesNoOpen.GetType() ? App.Current.MainWindow : null;
                msgYesNoOpen.ShowDialog();
                msgYesNoOpen.Activate();
                msgYesNoOpen.Focus();
                if (msgYesNoOpen.IsCancel == true)
                    ret = WindowCloseStatus.Cancel;
                else if (msgYesNoOpen.YesNo)
                    ret = WindowCloseStatus.Yes;
                else
                    ret = WindowCloseStatus.No;
            }
            else if (info.WindowType.ToString().ToUpper() == WindowInfo.MESSAGE_WINDOW.InfoOK.ToString().ToUpper())
            {
                var msgOK = new DmmInfoWindowOK(info.Info(), info.Context(), $"[{info.WindowId()}] {info.WindowTitle()}", true);
                msgOK.Button_1.Content = info.Button_1();
                msgOK.Owner = App.Current.MainWindow.GetType() != msgOK.GetType() ? App.Current.MainWindow : null;
                msgOK.ShowDialog();
                msgOK.Activate();
                msgOK.Focus();
                if (msgOK.IsCancel == true)
                    ret = WindowCloseStatus.Cancel;
                else if (msgOK.OK)
                    ret = WindowCloseStatus.Yes;
                else
                    ret = WindowCloseStatus.No;
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public static async Task<WindowCloseStatus> DMMWindowOpenAsync(int message_no, List<string> replaceList = null, object obj = null, string path = "", bool yesno = false)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"message_no:{message_no}, replaceList:{Util.GetListToParamString(replaceList)}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            WindowCloseStatus ret = WindowCloseStatus.None;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                ret = DMMWindowOpen(message_no, replaceList, obj, path, yesno);
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ret = DMMWindowOpen(message_no, replaceList, obj, path, yesno);
                });
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public static async Task<int> DMMWindowChoiceOpenAsync(
            List<int> message_no_list, int message_no_cancel = -1, List<string> replaceList = null, List<string> dataList = null)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"message_no_list:{Util.GetListToParamString(message_no_list)}, message_no_cancel:{message_no_cancel}, replaceList:{Util.GetListToParamString(replaceList)}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            int ret = -1;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                ret = DMMWindowChoiceOpen(message_no_list, message_no_cancel, replaceList, dataList);
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ret = DMMWindowChoiceOpen(message_no_list, message_no_cancel, replaceList, dataList);
                });
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End. Result:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="message_no_list"></param>
        /// <param name="message_no_cancel"></param>
        /// <param name="replaceList"></param>
        /// <returns></returns>
        public static int DMMWindowChoiceOpen(List<int> message_no_list, int message_no_cancel = -1, List<string> replaceList = null, List<string> dataList = null)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"message_no_list:{Util.GetListToParamString(message_no_list)}, message_no_cancel:{message_no_cancel}, replaceList:{Util.GetListToParamString(replaceList)}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = -1;

            List<WindowInfo> infoList = new();
            var choices = new List<DmmChoiceModel>();
            for (var i = 0; i < message_no_list.Count - 1; i++)
            {
                var info = WindowList.MessageWindowNo(message_no_list[i], replaceList);
                if (info.WindowType.ToString().ToUpper() == WindowInfo.MESSAGE_WINDOW.Choice.ToString().ToUpper())
                {
                    DmmChoiceModel item1 = new DmmChoiceModel() { MessageInfo = info.Info(), MessageText = info.Context().Replace("{data}", dataList?[i]), Index = i };
                    choices.Add(item1);
                }
                infoList.Add(WindowList.MessageWindowNo(message_no_list[i], replaceList));
            }
            var choice_button = WindowList.MessageWindowNo(message_no_list[message_no_list.Count - 1]);
            do
            {
                DmmChoiceWindow choiceWindow = new(choices, $"[{infoList[0].WindowId()}] {infoList[0].WindowTitle()}");
                choiceWindow.Owner = App.Current.MainWindow.GetType() != choiceWindow.GetType() ? App.Current.MainWindow : null;
                choiceWindow.Button_1.Content = choice_button.Info();
                choiceWindow.ShowDialog();
                choiceWindow.Activate();
                choiceWindow.Focus();
                if (choiceWindow.cancel)
                {
                    if (message_no_cancel == -1)
                    {
                        choiceWindow.Close();
                        break;
                    }
                    else
                    {
                        var resultWindow = WindowHelper.DMMWindowOpenAsync(message_no_cancel);
                        if (resultWindow.Result == WindowHelper.WindowCloseStatus.Yes)
                        {
                            choiceWindow.Close();
                            break;
                        }
                    }
                }
                else
                {
                    ret = choiceWindow.choice;
                }

            } while (ret == -1);

            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message_no"></param>
        /// <param name="replaceList"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static MessageBoxResult MessageBoxOpen(int message_no, List<string> replaceList = null, MessageBoxButton type = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, Window window = null)
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"message_no:{message_no}, replaceList:{Util.GetListToParamString(replaceList)}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            WindowInfo info = WindowList.MessageWindowNo(message_no);
            info.replaceList = replaceList;
            MessageBoxResult ret;
            if (Application.Current?.MainWindow is MainWindow)
            {
                // Once the main window is shown, use the same window style as the other messages
                ret = Application.Current.Dispatcher.Invoke(() => StyledMessageBox(info, type));
            }
            else if (window == null)
            {
                ret = MessageBox.Show(info.Info(), $"{info.WindowTitle()}", type, icon);
            }
            else
            {
                ret = App.Current.Dispatcher.Invoke(() => MessageBox.Show(info.Info(), $"{info.WindowTitle()}", type, icon));
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"Start.", $"Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        private static MessageBoxResult StyledMessageBox(WindowInfo info, MessageBoxButton type)
        {
            var title = $"[{info.WindowId()}] {info.WindowTitle()}";
            var owner = Application.Current.MainWindow;
            if (type == MessageBoxButton.YesNo || type == MessageBoxButton.OKCancel)
            {
                var yesNo = type == MessageBoxButton.YesNo;
                var window = new DmmMessageWindowYesNo(info.Info(), info.Context(), title) { Owner = owner };
                window.Button_1.Content = ButtonText(yesNo ? "Yes" : "OK", yesNo ? "はい" : "OK");
                window.Button_2.Content = ButtonText(yesNo ? "No" : "Cancel", yesNo ? "いいえ" : "キャンセル");
                window.ShowDialog();
                if (window.YesNo) return yesNo ? MessageBoxResult.Yes : MessageBoxResult.OK;
                return yesNo ? MessageBoxResult.No : MessageBoxResult.Cancel;
            }
            var ok = new DmmMessageWindowOK(info.Info(), info.Context(), title, true) { Owner = owner };
            ok.MessageInfo.Text = info.Info();
            ok.MessageText.Text = info.Context();
            if (string.IsNullOrEmpty(ok.MessageText.Text)) ok.MessageText.Visibility = Visibility.Collapsed;
            ok.Button_1.Content = ButtonText("OK", "OK");
            ok.ShowDialog();
            return MessageBoxResult.OK;
        }

        private static string ButtonText(string english, string japanese) =>
            Global.ConfigToml?.Language == "JP" ? japanese : UiTranslator.T(english);
    }
}
