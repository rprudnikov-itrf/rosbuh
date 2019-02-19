using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ozeki.Media;
using Ozeki.Media.MediaHandlers;
using Ozeki.Network.Nat;
using Ozeki.VoIP;
using Ozeki.VoIP.Media;
using Ozeki.VoIP.SDK;
using Ozeki.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using System.Data;
using System.Windows.Media;
using System.Net.NetworkInformation;
using System.Linq;
using System.Media;
using Ozeki.Media.Audio;
using System.ComponentModel;
using System.Collections.Generic;
using System.Net;
using Microsoft.Win32;
using RosApplication.UI.Windows;


namespace RosControl.UI
{
    public class PhoneControl : Control
    {
        //private object disposeLock = new System.Object();

        #region SDK
        private ISoftPhone softPhone;
        private IPhoneLine phoneLine;
        //private PhoneLineState phoneLineInformation;
        private IPhoneCall call;

        private Microphone microphone;
        private Speaker speaker;
        private MediaConnector connector;
        private PhoneCallAudioSender mediaSender;
        private PhoneCallAudioReceiver mediaReceiver;

        //private AudioQualityEnhancer audioProcessor = new AudioQualityEnhancer();
        //private AudioMixerMediaHandler outgoingDataMixer = new AudioMixerMediaHandler();

        private SoundPlayer ringbackPlayer = new SoundPlayer(RosApplication.Properties.Resources.ringback);
        private SoundPlayer ringtonePlayer = new SoundPlayer(RosApplication.Properties.Resources.ringtone);

        private Stopwatch watchCalling;
        private DispatcherTimer timerCalling;
        private bool IsHold;
        #endregion

        #region events

        public event RoutedEventHandler InCall
        {
            add { AddHandler(InCallEvent, value); }
            remove { RemoveHandler(InCallEvent, value); }
        }
        public static readonly RoutedEvent InCallEvent = EventManager.RegisterRoutedEvent(
            "InCall", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PhoneControl));


        public event RoutedEventHandler InCallAccept
        {
            add { AddHandler(InCallAcceptEvent, value); }
            remove { RemoveHandler(InCallAcceptEvent, value); }
        }
        public static readonly RoutedEvent InCallAcceptEvent = EventManager.RegisterRoutedEvent(
            "InCallAccept", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PhoneControl));





        public event RoutedEventHandler OutCall
        {
            add { AddHandler(OutCallEvent, value); }
            remove { RemoveHandler(OutCallEvent, value); }
        }
        public static readonly RoutedEvent OutCallEvent = EventManager.RegisterRoutedEvent(
            "OutCall", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PhoneControl));


        public event RoutedEventHandler OutCallAccept
        {
            add { AddHandler(OutCallAcceptEvent, value); }
            remove { RemoveHandler(OutCallAcceptEvent, value); }
        }
        public static readonly RoutedEvent OutCallAcceptEvent = EventManager.RegisterRoutedEvent(
            "OutCallAccept", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PhoneControl));




        public event RoutedEventHandler Calling
        {
            add { AddHandler(CallingEvent, value); }
            remove { RemoveHandler(CallingEvent, value); }
        }
        public static readonly RoutedEvent CallingEvent = EventManager.RegisterRoutedEvent(
            "Calling", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PhoneControl));


        public event RoutedEventHandler HandUping
        {
            add { AddHandler(HandUpingEvent, value); }
            remove { RemoveHandler(HandUpingEvent, value); }
        }
        public static readonly RoutedEvent HandUpingEvent = EventManager.RegisterRoutedEvent(
            "HandUping", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PhoneControl));



        #endregion

        public PhoneState State
        {
            get { return (PhoneState)GetValue(StateProperty); }
            set { SetValue(StateProperty, value); }
        }
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register("State", typeof(PhoneState), typeof(PhoneControl), new UIPropertyMetadata(PhoneState.Default));


        public string MyNumber
        {
            get { return (string)GetValue(MyNumberProperty); }
            set { SetValue(MyNumberProperty, value); }
        }
        public static readonly DependencyProperty MyNumberProperty =
            DependencyProperty.Register("MyNumber", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));


        public string DisplayName
        {
            get { return (string)GetValue(DisplayNameProperty); }
            set { SetValue(DisplayNameProperty, value); }
        }
        public static readonly DependencyProperty DisplayNameProperty =
            DependencyProperty.Register("DisplayName", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));




        public string MicrophoneDevice
        {
            get { return (string)GetValue(MicrophoneDeviceProperty); }
            set { SetValue(MicrophoneDeviceProperty, value); }
        }
        public static readonly DependencyProperty MicrophoneDeviceProperty =
            DependencyProperty.Register("MicrophoneDevice", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null, new PropertyChangedCallback(MicrophoneDevicePropertyChanged)));
        private static void MicrophoneDevicePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as PhoneControl;
            if (obj == null) return;

            if (e.NewValue != null)
            {
                var divice = Microphone.GetDevices().FirstOrDefault(p => p.ProductName.Equals(e.NewValue));
                if (divice != null) obj.microphone = Microphone.GetDevice(divice);
            }

            if (obj.microphone == null)
                obj.microphone = Microphone.GetDefaultDevice();
        }

        public System.Collections.Generic.IEnumerable<string> MicrophoneDevices
        {
            get { return (System.Collections.Generic.IEnumerable<string>)GetValue(MicrophoneDevicesProperty); }
            set { SetValue(MicrophoneDevicesProperty, value); }
        }
        public static readonly DependencyProperty MicrophoneDevicesProperty =
            DependencyProperty.Register("MicrophoneDevices", typeof(IEnumerable<string>), typeof(PhoneControl), new UIPropertyMetadata(Microphone.GetDevices().Select(p => p.ProductName).ToArray()));




        public string SpeakerDevice
        {
            get { return (string)GetValue(SpeakerDeviceProperty); }
            set { SetValue(SpeakerDeviceProperty, value); }
        }
        public static readonly DependencyProperty SpeakerDeviceProperty =
            DependencyProperty.Register("SpeakerDevice", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null, new PropertyChangedCallback(SpeakerDevicePropertyChanged)));
        private static void SpeakerDevicePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as PhoneControl;
            if (obj == null) return;


            if (e.NewValue != null)
            {
                var divice = Speaker.GetDevices().FirstOrDefault(p => p.ProductName.Equals(e.NewValue));
                if (divice != null) obj.speaker = Speaker.GetDevice(divice);
            }

            if (obj.speaker == null)
                obj.speaker = Speaker.GetDefaultDevice();
        }




        public IEnumerable<string> SpeakerDevices
        {
            get { return (IEnumerable<string>)GetValue(SpeakerDevicesProperty); }
            set { SetValue(SpeakerDevicesProperty, value); }
        }
        public static readonly DependencyProperty SpeakerDevicesProperty =
            DependencyProperty.Register("SpeakerDevices", typeof(IEnumerable<string>), typeof(PhoneControl), new UIPropertyMetadata(Speaker.GetDevices().Select(p => p.ProductName).ToArray()));






        public string Server
        {
            get { return (string)GetValue(ServerProperty); }
            set { SetValue(ServerProperty, value); }
        }
        public static readonly DependencyProperty ServerProperty =
            DependencyProperty.Register("Server", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));



        public string ServerProxy
        {
            get { return (string)GetValue(ServerProxyProperty); }
            set { SetValue(ServerProxyProperty, value); }
        }
        public static readonly DependencyProperty ServerProxyProperty =
            DependencyProperty.Register("ServerProxy", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));



        public string Login
        {
            get { return (string)GetValue(LoginProperty); }
            set { SetValue(LoginProperty, value); }
        }
        public static readonly DependencyProperty LoginProperty =
            DependencyProperty.Register("Login", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));

        public string Password
        {
            get { return (string)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register("Password", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));


        public string Protocol
        {
            get { return (string)GetValue(ProtocolProperty); }
            set { SetValue(ProtocolProperty, value); }
        }
        public static readonly DependencyProperty ProtocolProperty =
            DependencyProperty.Register("Protocol", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));










        public string Number
        {
            get { return (string)GetValue(NumberProperty); }
            set { SetValue(NumberProperty, value); }
        }
        public static readonly DependencyProperty NumberProperty =
            DependencyProperty.Register("Number", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null, new PropertyChangedCallback(NumberPropertyChanged)));

        private static void NumberPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as PhoneControl;
            if (obj == null) return;

            obj.SetValue(NumberViewPropertyKey, RosControl.UI.Phone.FormatNumber(e.NewValue as string, false));
        }


        public string NumberView
        {
            get { return (string)GetValue(NumberViewPropertyKey.DependencyProperty); }
        }
        public static readonly DependencyPropertyKey NumberViewPropertyKey =
            DependencyProperty.RegisterReadOnly("NumberView", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));



        public bool IsRegister
        {
            get { return (bool)GetValue(IsRegisterPropertyKey.DependencyProperty); }
        }
        public static readonly DependencyPropertyKey IsRegisterPropertyKey =
            DependencyProperty.RegisterReadOnly("IsRegister", typeof(bool), typeof(PhoneControl), new UIPropertyMetadata(false));



        public string StateInfo
        {
            get { return (string)GetValue(StateInfoProperty); }
            set { SetValue(StateInfoProperty, value); }
        }
        public static readonly DependencyProperty StateInfoProperty =
            DependencyProperty.Register("StateInfo", typeof(string), typeof(PhoneControl), new UIPropertyMetadata(null));




        static PhoneControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PhoneControl), new FrameworkPropertyMetadata(typeof(PhoneControl)));
            KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(PhoneControl), new FrameworkPropertyMetadata(KeyboardNavigationMode.Once));
            KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(typeof(PhoneControl), new FrameworkPropertyMetadata(KeyboardNavigationMode.Contained));

            CommandManager.RegisterClassCommandBinding(typeof(PhoneControl), new CommandBinding(ApplicationCommands.Find, Find_Execute));
            CommandManager.RegisterClassCommandBinding(typeof(PhoneControl), new CommandBinding(ApplicationCommands.New, New_Execute, CanExecute));
            CommandManager.RegisterClassCommandBinding(typeof(PhoneControl), new CommandBinding(ApplicationCommands.Delete, Remove_Execute, CanExecute));
            CommandManager.RegisterClassCommandBinding(typeof(PhoneControl), new CommandBinding(ApplicationCommands.Open, Call_Execute));
            CommandManager.RegisterClassCommandBinding(typeof(PhoneControl), new CommandBinding(ApplicationCommands.Close, HandUp_Execute));
            CommandManager.RegisterClassCommandBinding(typeof(PhoneControl), new CommandBinding(ApplicationCommands.NotACommand, NotACommand_Execute));
        }

        public PhoneControl()
        {
            //audioProcessor.SetEchoSource(speaker);
            //audioProcessor.NoiseReductionLevel = Ozeki.Media.DSP.NoiseReductionLevel.Medium;
            //audioProcessor.AutoGainControl = true;

            #region some devices are missing
            //DispatcherInvoke(() => 
            //{
            //    var message = String.Empty;

            //    if (microphone == null)
            //        message += "У вас нет микрофона, подключенного к компьютеру, обратите внимание, что ваш партнер не будет слышать ваш голос.\n";
            //    if (speaker == null)
            //        message += "У вас нет динамиков, подключенных к компьютеру, пожалуйста, обратите внимание, что вы не будете слышать своего партнера.\n";

            //    if (message != String.Empty)
            //        MessageBox.Show(message, "Телефон", MessageBoxButton.OK, MessageBoxImage.Information);
            //});
            #endregion

            timerCalling = new DispatcherTimer();
            timerCalling.Interval = TimeSpan.FromMilliseconds(500);
            timerCalling.Tick += new EventHandler(timerCalling_Tick);

            LoadSettings();
        }

        private void timerCalling_Tick(object sender, EventArgs e)
        {
            try
            {
                if (watchCalling != null)
                {
                    if (watchCalling.Elapsed.Hours > 0)
                        StateInfo = string.Format("{0:00.}:{1:00}:{2:00}", watchCalling.Elapsed.Hours, watchCalling.Elapsed.Minutes, watchCalling.Elapsed.Seconds);
                    else
                        StateInfo = string.Format("{0:00}:{1:00}", watchCalling.Elapsed.Minutes, watchCalling.Elapsed.Seconds);
                }
            }
            catch
            {
            }
        }

        //private FrameworkElement keyHost;
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Focus();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            try
            {
                if (IsRegister)
                {
                    var keys = new System.Collections.Generic.Dictionary<Key, string>();
                    keys.Add(Key.NumPad0, "0");
                    keys.Add(Key.NumPad1, "1");
                    keys.Add(Key.NumPad2, "2");
                    keys.Add(Key.NumPad3, "3");
                    keys.Add(Key.NumPad4, "4");
                    keys.Add(Key.NumPad5, "5");
                    keys.Add(Key.NumPad6, "6");
                    keys.Add(Key.NumPad7, "7");
                    keys.Add(Key.NumPad8, "8");
                    keys.Add(Key.NumPad9, "9");

                    if ((State == PhoneState.Default || State == PhoneState.Call) && keys.ContainsKey(e.Key))
                        AddNumber(keys[e.Key]);

                    else if (State == PhoneState.Default && (e.Key == Key.Back || e.Key == Key.Delete))
                        RemoveNumber();

                    else if (e.Key == Key.Return)
                    {
                        switch (State)
                        {
                            case PhoneState.InCall:
                            case PhoneState.Default:
                                Call();
                                break;

                            case PhoneState.Call:
                                HandUp();
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        private static void CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (sender is PhoneControl)
            {
                e.CanExecute = ((PhoneControl)sender).IsRegister;
                e.Handled = true;
                //CommandManager.InvalidateRequerySuggested();
            }
        }
        private static void NotACommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (sender is PhoneControl)
                {
                    switch (e.Parameter as string)
                    {
                        case "connect":
                            ((PhoneControl)sender).Connect();
                            break;

                        case "disconnect":
                            Phone.PhoneAutoConnect = false;
                            ((PhoneControl)sender).Disonnect();
                            break;

                        case "hold":
                            {
                                var obj = ((PhoneControl)sender);
                                var call = obj.call;
                                if (call != null)
                                {
                                    var button = e.Source as Button;
                                    if (obj.IsHold)
                                    {
                                        call.ToggleHold();

                                        if (button != null)
                                            button.Style = obj.TryFindResource("PhoneYellow") as Style;
                                    }
                                    else
                                    {
                                        call.HoldCall();

                                        if (button != null)
                                            button.Style = obj.TryFindResource("PhoneGreen") as Style;
                                    }

                                    obj.IsHold = !obj.IsHold;
                                }
                            }
                            break;

                        case "transfer":
                            {
                                var obj = ((PhoneControl)sender);
                                var dialog = new TransferWindow();
                                if (dialog.ShowDialog().GetValueOrDefault())
                                {
                                    obj.Call(dialog.PhoneNumber);
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }
        private static void HandUp_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (sender is PhoneControl)
                    ((PhoneControl)sender).HandUp();
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }
        private static void Call_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (sender is PhoneControl)
                    ((PhoneControl)sender).Call(Convert.ToString(e.Parameter ?? ((PhoneControl)sender).Number));
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }
        private static void Find_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is PhoneControl)
            {
                new RosApplication.UI.Windows.RecordsWindow()
                {
                    Sip = ((PhoneControl)sender).MyNumber
                }.Show();
            }
        }
        private static void New_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (sender is PhoneControl)
                {
                    ((PhoneControl)sender).AddNumber(e.Parameter as string);
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }
        private static void Remove_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (sender is PhoneControl)
                {
                    ((PhoneControl)sender).RemoveNumber();
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        private void AddNumber(string num)
        {
            try
            {
                if (!string.IsNullOrEmpty(num) && (Number ?? string.Empty).Length < 20)
                {
                    Number += num;
                    StateInfo = null;

                    if (call != null && call.CallState == CallState.InCall)
                    {
                        int id;
                        if ("*".Equals(num))
                        {
                            call.StartDTMFSignal(DtmfNamedEvents.DtmfStar);
                            call.StopDTMFSignal(DtmfNamedEvents.DtmfStar);
                        }
                        else if ("#".Equals(num))
                        {
                            call.StartDTMFSignal(DtmfNamedEvents.DtmfHashMark);
                            call.StopDTMFSignal(DtmfNamedEvents.DtmfHashMark);
                        }
                        else if (int.TryParse(num, out id))
                        {
                            call.StartDTMFSignal((DtmfNamedEvents)id);
                            call.StopDTMFSignal((DtmfNamedEvents)id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }
        private void RemoveNumber()
        {
            try
            {
                if ((Number ?? string.Empty).Length > 0)
                {
                    Number = Number.Substring(0, Number.Length - 1);
                }
                else
                {
                    Number = null;
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }
        public void Call(string number)
        {
            if (!IsRegister)
                return;

            try
            {
                //при уже разговоре можно сделать трансфер
                if (State == PhoneState.Call)
                {
                    var _number = Regex.Replace(number ?? "", @"[^\d#*]", "", RegexOptions.IgnoreCase);
                    if (!string.IsNullOrEmpty(_number) && MessageBox.Show(string.Format("Вы действительно хотите перевести звонок на номер {0}?", number), "Предупреждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        if (call != null)
                            call.BlindTransfer(_number);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }

            Number = Regex.Replace(number ?? "", @"[^\d#*]", "", RegexOptions.IgnoreCase);
            Call();
        }
        public void Call()
        {
            try
            {
                if (State == PhoneState.InCall)
                {
                    try
                    {
                        State = PhoneState.Call;
                        if (call != null)
                            call.Accept();
                    }
                    catch(Exception ex)
                    {
                        if (ex != null)
                        {
                        }
                    }
                    finally
                    {
                        //поставить статус занят
                        DoNotDisturb(true);
                    }

                    RaiseEvent(new RoutedEventArgs(InCallAcceptEvent));
                    RaiseEvent(new RoutedEventArgs(Phone.InCallAcceptEvent));
                    return;
                }

                if (call != null)
                    return;

                if (string.IsNullOrEmpty(Number) || !IsRegister)
                    return;

                State = PhoneState.Call;
                RaiseEvent(new RoutedEventArgs(OutCallEvent));
                RaiseEvent(new RoutedEventArgs(Phone.OutCallEvent));

                RaiseEvent(new RoutedEventArgs(CallingEvent));
                RaiseEvent(new RoutedEventArgs(Phone.CallingEvent));


                var _number = Number;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        //lock (disposeLock)
                        {
                            if (softPhone != null)
                            {

                                call = softPhone.CreateCallObject(phoneLine, _number, CallType.Audio);
                                WireUpCallEvents(call);
                                if (call != null)
                                    call.Start();

                                DispatcherInvoke(() =>
                                {
                                    //поставить статус занят
                                    DoNotDisturb(true);

                                    RaiseEvent(new RoutedEventArgs(OutCallAcceptEvent));
                                    RaiseEvent(new RoutedEventArgs(Phone.OutCallAcceptEvent));
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        StateInfo = ex.Message;
                        RosApplication.App.AddLog(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        private static object DoNotDisturbLock = new System.Object();
        public void DoNotDisturb(bool value)
        {
            DispatcherInvoke(() =>
            {
                try
                {
                    lock (DoNotDisturbLock)
                    {
                        if (phoneLine != null && phoneLine.DoNotDisturb != value)
                            phoneLine.DoNotDisturb = value;
                    }
                }
                catch (Exception ex)
                {
                    RosApplication.App.AddLog(ex);
                }
            });
        }


        public void HandUp()
        {
            try
            {
                StopRingPlayers();

                if (call != null)
                {
                    if (State == PhoneState.InCall && call.CallState == CallState.Ringing)
                    {
                        System.Threading.Tasks.Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                try
                                {
                                    if (call != null)
                                    {
                                        call.Reject();
                                        call = null;
                                    }
                                }
                                finally
                                {
                                    //поставить статус занят
                                    DoNotDisturb(false);
                                }
                            }
                            catch (Exception ex)
                            {
                                //RosApplication.App.AddLog(ex);
                            }
                        });
                    }
                    else
                    {
                        System.Threading.Tasks.Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                try
                                {
                                    if (call != null)
                                    {
                                        call.HangUp();
                                        call = null;
                                    }
                                }
                                finally
                                {
                                    //поставить статус занят
                                    DoNotDisturb(false);
                                }
                            }
                            catch (Exception ex)
                            {
                                //RosApplication.App.AddLog(ex);
                            }
                        });
                    }

                    State = PhoneState.Default;
                }
                Number = null;
                DisplayName = null;
                StateInfo = string.Empty;
                State = PhoneState.Default;

                RaiseEvent(new RoutedEventArgs(HandUpingEvent));
                RaiseEvent(new RoutedEventArgs(Phone.HandUpingEvent));
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }




        #region Ozeki VoIP-SIP SDK's events
        /// <summary>
        /// Occurs when phone linde state has changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void phoneLine_PhoneLineInformation(object sender, VoIPEventArgs<PhoneLineState> e)
        {
            try
            {
                if (e != null)
                {
                    var phoneLineInformation = e.Item;
                    DispatcherInvoke(() =>
                    {
                        try
                        {
                            SetValue(IsRegisterPropertyKey, false);
                            //CommandManager.InvalidateRequerySuggested();

                            switch (phoneLineInformation)
                            {
                                case PhoneLineState.Initialized:
                                case PhoneLineState.RegistrationRequested:
                                    StateInfo = "Авторизация";
                                    break;

                                case PhoneLineState.RegistrationSucceeded:
                                    //сохранить в регистр последний успешный IP
                                    var succesIp = listIPAdress.ElementAtOrDefault(selectedIP);
                                    if (succesIp != null)
                                        LastSuccesIP = succesIp.ToString();


                                    if (((IPhoneLine)sender).SIPAccount != null)
                                        MyNumber = ((IPhoneLine)sender).SIPAccount.RegisterName;
                                    //StateInfo = "Мой номер";
                                    StateInfo = string.Empty;
                                    SetValue(IsRegisterPropertyKey, true);
                                    //CommandManager.InvalidateRequerySuggested();
                                    break;

                                case PhoneLineState.RegistrationTimedOut:
                                default:

                                    //если есть ещё IP то пробуем
                                    if (++selectedIP < listIPAdress.Count)
                                    {
                                        Initialize(true);
                                        return;
                                    }

                                    MyNumber = "Error";
                                    StateInfo = phoneLineInformation.ToString();
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            RosApplication.App.AddLog(ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        /// <summary>
        /// Occurs when an incoming call request has received.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void softPhone_IncomingCall(object sender, VoIPEventArgs<IPhoneCall> e)
        {
            try
            {
                var call = e.Item;
                if (call == null)
                    return;

                DispatcherInvoke(() =>
                {
                    try
                    {
                        if (call != null)
                        {
                            //если я уже говорю то при входящем звонке давать отсечку
                            if (State != PhoneState.Default)
                            {
                                //call.HangUp();
                                return;
                            }

                            try
                            {
                                if (ringtonePlayer != null)
                                    ringtonePlayer.PlayLooping();

                                State = PhoneState.InCall;
                                if (call.DialInfo != null)
                                {
                                    Number = Regex.Replace(call.DialInfo.UserName, @"[^\d#*]", "", RegexOptions.IgnoreCase);
                                }
                                StateInfo = string.Empty;

                                //выделить вкладку если фокус на другой
                                var frame = RosControl.Helper.FindParentControl<КонтентПанель>(this);
                                if (frame != null)
                                {
                                    var tab = RosControl.Helper.FindParentLogicalControl<DocumentsTabItem>(frame);
                                    if (tab != null) tab.Focus();
                                }
                            }
                            finally
                            {
                                DoNotDisturb(true);
                            }

                            RaiseEvent(new RoutedEventArgs(InCallEvent));
                            RaiseEvent(new RoutedEventArgs(Phone.InCallEvent));

                            RaiseEvent(new RoutedEventArgs(CallingEvent));
                            RaiseEvent(new RoutedEventArgs(Phone.CallingEvent));

                            this.call = call;
                            WireUpCallEvents(this.call);
                        }
                    }
                    catch (Exception ex)
                    {
                        RosApplication.App.AddLog(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        /// <summary>
        /// Occurs when the phone call state has changed.
        /// Example: Ringing, Incall, Completed, Cancelled ...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void call_CallStateChanged(object sender, VoIPEventArgs<CallState> e)
        {
            try
            {
                IPhoneCall call = sender as IPhoneCall;
                if (call == null)
                    return;

                //остановить таймеры
                DispatcherInvoke(() =>
                {
                    if (timerCalling != null && timerCalling.IsEnabled)
                        timerCalling.Stop();

                    if (watchCalling != null)
                    {
                        watchCalling.Stop();
                        watchCalling = null;
                    }
                });

                switch (e.Item)
                {
                    case CallState.InCall:
                        try
                        {
                            StopRingPlayers();

                            if (microphone != null)
                                microphone.Start();

                            ////connector.Connect(microphone, audioProcessor);
                            ////connector.Connect(audioProcessor, outgoingDataMixer);
                            ////connector.Connect(outgoingDataMixer, mediaSender);
                            //connector.Connect(microphone, mediaSender);


                            if (speaker != null)
                                speaker.Start();
                            //connector.Connect(mediaReceiver, speaker);

                            mediaSender.AttachToCall(call);
                            mediaReceiver.AttachToCall(call);

                            DispatcherInvoke(() =>
                            {
                                StateInfo = "00:00";
                                watchCalling = Stopwatch.StartNew();
                                timerCalling.Start();
                            });
                        }
                        finally
                        {
                            //поставить статус занят
                            //DoNotDisturb(true);
                        }
                        break;

                    case CallState.Completed:
                        try
                        {
                            StopRingPlayers();

                            //if (microphone != null)
                            //    microphone.Stop();

                            //connector.Disconnect(microphone, mediaSender);
                            ////connector.Disconnect(outgoingDataMixer, mediaSender);
                            ////connector.Disconnect(audioProcessor, outgoingDataMixer);
                            ////connector.Disconnect(microphone, audioProcessor);

                            //if (speaker != null)
                            //    speaker.Stop();

                            //connector.Disconnect(mediaReceiver, speaker);

                            mediaSender.Detach();
                            mediaReceiver.Detach();

                            WireDownCallEvents(call);
                            this.call = null;

                            DispatcherInvoke(() =>
                            {
                                State = PhoneState.Default;
                                Number = null;
                                DisplayName = null;
                                StateInfo = string.Empty;
                            });
                        }
                        finally
                        {
                            //поставить статус занят
                            DoNotDisturb(false);
                        }
                        break;

                    case CallState.Busy:
                        DispatcherInvoke(() =>
                        {
                            StopRingPlayers();
                            StateInfo = "Занято";
                        });
                        break;

                    case CallState.Setup:
                        DispatcherInvoke(() =>
                        {
                            StateInfo = "Набор номера";
                        });
                        break;

                    case CallState.Ringing:
                        DispatcherInvoke(() =>
                        {
                            StateInfo = "Звоним";
                            if (State == PhoneState.Call)
                                ringbackPlayer.PlayLooping();
                        });
                        break;

                    case CallState.Cancelled:
                        try
                        {
                            WireDownCallEvents(call);
                            this.call = null;
                            DispatcherInvoke(() =>
                            {
                                StopRingPlayers();
                                State = PhoneState.Default;
                                Number = null;
                                DisplayName = null;
                                StateInfo = string.Empty;
                            });
                        }
                        finally
                        {
                            //поставить статус занят
                            DoNotDisturb(false);
                        }
                        break;

                    default:
                        DispatcherInvoke(() => { StopRingPlayers(); /*StateInfo = e.Item.ToString();*/ });
                        break;
                }
            }
            catch (Exception ex)
            {
                //try
                //{
                //    DispatcherInvoke(() =>
                //    {
                //        if (ex != null)
                //        {
                //            StateInfo = ex.Message;
                //        }
                //    });
                //}
                //catch
                //{
                //}
                //finally
                //{
                //    RosApplication.App.AddLog(ex);
                //}
            }
        }

        /// <summary>
        /// Display DTMF signals.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void call_DtmfReceived(object sender, VoIPEventArgs<DtmfInfo> e)
        {
            if (e != null && e.Item != null)
            {
                DtmfSignal signal = e.Item.Signal;
                DispatcherInvoke(() =>
                {
                    if (signal != null)
                        StateInfo = String.Format("DTMF signal received: {0} ", signal.Signal);
                });
            }
        }

        /// <summary>
        /// There are certain situations when the call cannot be created, for example the dialed number is not available 
        /// or maybe there is no endpoint to the dialed PBX, or simply the telephone line is busy. 
        /// This event handling is for displaying these events.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void call_CallErrorOccured(object sender, VoIPEventArgs<CallError> e)
        {
            DispatcherInvoke(() =>
            {
                if (e != null)
                {
                    StateInfo = e.Item.ToString();
                }
            });
        }

        private void StopRingPlayers()
        {
            try
            {
                if (ringbackPlayer != null)
                    ringbackPlayer.Stop();

                if (ringtonePlayer != null)
                    ringtonePlayer.Stop();
            }
            catch (Exception ex)
            {
                //RosApplication.App.AddLog(ex);
            }
        }
        #endregion

        #region Helper Functions
        private void LoadSettings()
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var server = client.Архив.ПолучитьКонстанту<string>("VoIP.Server");
                        var proxy  = client.Архив.ПолучитьКонстанту<string>("VoIP.Server.Proxy");
                        var protocol = client.Архив.ПолучитьКонстанту<string>("VoIP.Protocol");
                        if (string.IsNullOrEmpty(protocol)) protocol = "TCP";

                        var account = client.Архив.ПолучитьЗначение(client.СведенияПользователя.id_node, new string[] { "SipНомер", "SipПароль", "SipMicrophone", "SipSpeaker" });
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            try
                            {
                                Server = server;
                                ServerProxy = proxy;
                                Protocol = protocol;
                                Login = account.Convert<string>("SipНомер");
                                Password = account.Convert<string>("SipПароль");

                                if (!string.IsNullOrEmpty(account.Convert<string>("SipMicrophone")))
                                {
                                    MicrophoneDevice = account.Convert<string>("SipMicrophone");
                                }
                                else
                                {
                                    var defaultDevice = Microphone.GetDefaultDevice();
                                    if (defaultDevice != null && defaultDevice.DeviceInfo != null)
                                        MicrophoneDevice = defaultDevice.DeviceInfo.ProductName;
                                }

                                if (!string.IsNullOrEmpty(account.Convert<string>("SipSpeaker")))
                                {
                                    SpeakerDevice = account.Convert<string>("SipSpeaker");
                                }
                                else
                                {
                                    var defaultDevice = Speaker.GetDefaultDevice();
                                    if (defaultDevice != null && defaultDevice.DeviceInfo != null)
                                        SpeakerDevice = defaultDevice.DeviceInfo.ProductName;
                                }

                                //MicrophoneDevice = !string.IsNullOrEmpty(account.Convert<string>("SipMicrophone")) ? account.Convert<string>("SipMicrophone") : Microphone.GetDefaultDevice().DeviceInfo.ProductName;
                                //SpeakerDevice = !string.IsNullOrEmpty(account.Convert<string>("SipSpeaker")) ? account.Convert<string>("SipSpeaker") : Speaker.GetDefaultDevice().DeviceInfo.ProductName;

                                if (Phone.PhoneAutoConnect)
                                {
                                    StateInfo = "Подключение";
                                    Initialize();
                                }
                                else
                                {
                                    StateInfo = "Выключен";
                                }
                            }
                            catch (Exception ex)
                            {
                                RosApplication.App.AddLog(ex);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            StateInfo = ex.Message;
                        });
                    }
                    catch
                    {
                    }
                    finally
                    {
                        //RosApplication.App.AddLog(ex);
                    }
                }
            });
        }

        private static RegistryKey Registry
        {
            get
            {
                return Microsoft.Win32.Registry.CurrentUser.CreateSubKey("itrf.ru");
            }
        }
        private static string _LastSuccesIP;
        public static string LastSuccesIP
        {
            get
            {
                if (string.IsNullOrEmpty(_LastSuccesIP))
                {
                    var itrf = Registry;
                    if (itrf == null)
                    {
                        return null;
                    }
                    _LastSuccesIP = Convert.ToString(itrf.GetValue("LastSuccesIP"));
                }

                return _LastSuccesIP;
            }

            set
            {
                if (_LastSuccesIP != value)
                {
                    _LastSuccesIP = value;

                    var itrf = Registry;
                    if (itrf != null)
                    {
                        itrf.SetValue("LastSuccesIP", value);
                    }
                }
            }
        }

        private static object lockInitialize = new System.Object();
        private static List<IPAddress> listIPAdress;
        private static int selectedIP;
        private static bool IsSetLicense;
        private void Initialize()
        {
            Initialize(false);
        }
        private void Initialize(bool reload)
        {
            try
            {
                Dispose();
                SetValue(IsRegisterPropertyKey, false);


                // При включение выключение при указание лицензии 2й раз пиштся ошибка 30000 фиксим ее
                if (!IsSetLicense)
                {
                    IsSetLicense = true;
                    Ozeki.VoIP.SDK.Protection.LicenseManager.Instance.SetLicense(
                        "OZSDK-RUS1CALL-130424-8C808F68",
                        "TUNDOjEsTVBMOjEsRzcyOTp0cnVlLE1TTEM6MSxNRkM6MSxVUDoyMDE0LjA0LjI0LFA6MjE5OS4wMS4wMXxtQUNNdXBHcVUzRndFVGtWSDdjVXo0TmlvLzNCRFpLY05qU2gyYXY2RWhmWjN1TDVBeUtlOUh5elFxcTdRbVdzNUhVY3IxQ3p1T3pjeXBtWWkyVVFYZz09");
                }

                //F:\Documents and Settings\All Users\Application Data\Ozeki.{20d04fe0-3aea-1069-a2d8-08002b30309d}\VoIP_SDK\
                //C:\ProgramData\Ozeki.{20d04fe0-3aea-1069-a2d8-08002b30309d}\VoIP_SDK\

                /*
                // теперь у нас лицензия
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var dir = System.IO.Path.Combine(programData, @"Ozeki.{20d04fe0-3aea-1069-a2d8-08002b30309d}\VoIP_SDK\");
                if (System.IO.Directory.Exists(dir))
                    System.IO.Directory.Delete(dir, true);
                 */

                lock (lockInitialize)
                {
                    if (listIPAdress == null || !reload)
                    {
                        if (string.IsNullOrEmpty(LastSuccesIP))
                            LastSuccesIP = SoftPhoneFactory.GetLocalIP().ToString();

                        selectedIP = 0;
                        listIPAdress = SoftPhoneFactory.GetAddressList();
                        var item = listIPAdress.FirstOrDefault(p => LastSuccesIP.Equals(p.ToString()));
                        if (item != null)
                        {
                            listIPAdress.Remove(item);
                            listIPAdress.Insert(0, item);
                        }
                    }
                }

                //SoftPhoneFactory.GetLocalIP().ToString()
                disposed = false;
                var ip = listIPAdress.ElementAt(selectedIP).ToString();
                var server = this.Server;
                var proxy = this.ServerProxy;
                var protocol = this.Protocol;
                var login = this.Login;
                var password = this.Password;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
                        {
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            {
                                MyNumber = null;
                                StateInfo = "Выключен";
                            });
                            return;
                        }

                        var serverToken = server.Split(':');
                        var host = serverToken.ElementAtOrDefault(0);
                        var port = !string.IsNullOrEmpty(serverToken.ElementAtOrDefault(1)) ? Convert.ToInt32(serverToken.ElementAtOrDefault(1)) : 5060;

                        softPhone = SoftPhoneFactory.CreateSoftPhone(ip, port, port + 10, port);
                        //softPhone.BeginNatDiscovery(ip, NatDiscoveryCallback);
                        //softPhone.BeginNatDiscovery(ip, "62.113.32.203", NatDiscoveryCallback);
                        softPhone.IncomingCall += new EventHandler<VoIPEventArgs<IPhoneCall>>(softPhone_IncomingCall);

                        //var sipAccount = new SIPAccount(true, login, login, login, password, server, 5060, server);
                        var transport = "UDP".Equals(protocol) ? Ozeki.Network.TransportType.Udp : Ozeki.Network.TransportType.Tcp;
                        var sipAccount = new SIPAccount(true, login, login, login, password, host, port, proxy);
                        var Configuration = new NatConfiguration(NatTraversalMethod.None);
                        //var Configuration = new NatConfiguration(NatTraversalMethod.STUN, "stun.zoiper.com");
                        phoneLine = softPhone.CreatePhoneLine(sipAccount, Configuration, transport, SRTPMode.None, 1800);
                        //phoneLine.KeepAliveMode = KeepAliveMode.REGISTER;
                        phoneLine.PhoneLineStateChanged += new EventHandler<VoIPEventArgs<PhoneLineState>>(phoneLine_PhoneLineInformation);
                        var G729 = softPhone.Codecs.FirstOrDefault(p => string.Equals(p.CodecName, "G729"));
                        if (G729 != null)
                            softPhone.EnableCodec(G729.PayloadType);

                        softPhone.RegisterPhoneLine(phoneLine);

                        InitAudio();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            {
                                StateInfo = ex.Message;
                            });
                        }
                        catch
                        {
                        }
                        finally
                        {
                            RosApplication.App.AddLog(ex);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                StateInfo = ex.Message;
                RosApplication.App.AddLog(ex);
            }
        }
        public void NatDiscoveryCallback(NatInfo info)
        {
            if (info.NatType == NatType.FullCone)
            {
            }
        }

        private void InitAudio()
        {
            if (connector == null)
                connector = new MediaConnector();

            if (mediaSender == null)
                mediaSender = new PhoneCallAudioSender();

            if (mediaReceiver == null)
                mediaReceiver = new PhoneCallAudioReceiver();

            //if (microphone != null)
            //    microphone.Start();

            //connector.Connect(microphone, audioProcessor);
            //connector.Connect(audioProcessor, outgoingDataMixer);
            //connector.Connect(outgoingDataMixer, mediaSender);
            if (microphone != null)
                connector.Connect(microphone, mediaSender);


            //if (speaker != null)
            //    speaker.Start();

            if (speaker != null)
                connector.Connect(mediaReceiver, speaker);
        }

        private void DisposeAudio()
        {
            //if (microphone != null && microphone.State != CaptureState.Stopped)
            //    microphone.Stop();

            if (connector != null && mediaSender != null && microphone != null)
                connector.Disconnect(microphone, mediaSender);

            ////connector.Disconnect(outgoingDataMixer, mediaSender);
            ////connector.Disconnect(audioProcessor, outgoingDataMixer);
            ////connector.Disconnect(microphone, audioProcessor);

            //if (speaker != null)
            //    speaker.Stop();

            if (connector != null && mediaReceiver != null && speaker != null)
                connector.Disconnect(mediaReceiver, speaker);
        }

        private string GatewayAddresses()
        {
            foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
            {
                var GetIPProperties = item.GetIPProperties();
                if (GetIPProperties.GatewayAddresses.Count > 0)
                {
                    return GetIPProperties.GatewayAddresses.First().Address.ToString();
                }
            }
            return string.Empty;
        }

        public void Connect()
        {
            try
            {
                Phone.PhoneAutoConnect = true;
                StateInfo = "Подключение";
                var login = this.Login;
                var password = this.Password;
                var microphoneDevice = this.MicrophoneDevice;
                var speakerDevice = this.SpeakerDevice;

                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            var v = new System.Collections.Generic.Dictionary<string, object>();
                            v.Add("SipНомер", login);
                            v.Add("SipПароль", password);
                            v.Add("SipMicrophone", microphoneDevice);
                            v.Add("SipSpeaker", speakerDevice);
                            client.Архив.СохранитьЗначение(client.СведенияПользователя.id_node, v, RosService.Data.Хранилище.Оперативное);

                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            {
                                Initialize();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            {
                                StateInfo = ex.Message;
                            });
                        }
                        catch
                        {
                        }
                        finally
                        {
                            //RosApplication.App.AddLog(ex);
                        }
                    }
                });

                var PART_TabControl = GetTemplateChild("PART_TabControl") as System.Windows.Controls.TabControl;
                if (PART_TabControl != null)
                {
                    PART_TabControl.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }
        public void Disonnect()
        {
            try
            {
                //Phone.PhoneAutoConnect = false;
                MyNumber = null;
                Number = null;
                DisplayName = null;
                StateInfo = "Выключен";

                SetValue(IsRegisterPropertyKey, false);

                var PART_TabControl = GetTemplateChild("PART_TabControl") as System.Windows.Controls.TabControl;
                if (PART_TabControl != null)
                {
                    PART_TabControl.SelectedIndex = 0;
                }

                Dispose();

                //System.Threading.Tasks.Task.Factory.StartNew(() =>
                //{
                //    try
                //    {
                //        Dispose();
                //    }
                //    catch (Exception ex)
                //    {
                //        try
                //        {
                //            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                //            {
                //                StateInfo = ex.Message;
                //            });
                //        }
                //        catch
                //        {
                //        }
                //        finally
                //        {
                //            //RosApplication.App.AddLog(ex);
                //        }
                //    }
                //});
            }
            catch (Exception ex)
            {
                //RosApplication.App.AddLog(ex);
            }
        }

        /// <summary>
        /// It signs up to the necessary events of a call transact.
        /// </summary>
        private void WireUpCallEvents(IPhoneCall call)
        {
            if (call != null)
            {
                call.CallStateChanged += (call_CallStateChanged);
                call.DtmfReceived += (call_DtmfReceived);
                call.CallErrorOccured += (call_CallErrorOccured);
            }
        }

        /// <summary>
        /// It signs down from the necessary events of a call transact.
        /// </summary>
        private void WireDownCallEvents(IPhoneCall call)
        {
            if (call != null)
            {
                call.CallStateChanged -= (call_CallStateChanged);
                call.DtmfReceived -= (call_DtmfReceived);
                call.CallErrorOccured -= (call_CallErrorOccured);
            }
        }

        private void DispatcherInvoke(Action action)
        {
            try
            {
                Dispatcher.Invoke(action);
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }
        #endregion




        private T FindParentControl<T>(DependencyObject outerDepObj) where T : DependencyObject
        {
            if (outerDepObj == null) return null;
            else if (outerDepObj is T) return outerDepObj as T;

            DependencyObject dObj = VisualTreeHelper.GetParent(outerDepObj);
            if (dObj == null)
                return null;

            while (dObj != null)
            {
                if (dObj is T)
                    return dObj as T;

                dObj = VisualTreeHelper.GetParent(dObj);
            }

            return null;
        }

        ~PhoneControl()
        {
            //Dispose();
        }

        private bool disposed = false;
        private void Dispose()
        {
            try
            {
                //lock (disposeLock)
                {
                    if (!disposed && softPhone != null)
                    {
                        disposed = true;
                        if (phoneLine != null)
                        {
                            phoneLine.PhoneLineStateChanged -= new EventHandler<VoIPEventArgs<PhoneLineState>>(phoneLine_PhoneLineInformation);
                            softPhone.UnregisterPhoneLine(phoneLine);
                            phoneLine.Dispose();
                            phoneLine = null;
                        }
                        softPhone.Close();
                        softPhone = null;

                        DisposeAudio();
                    }
                }
            }
            catch (Exception ex)
            {
                //RosApplication.App.AddLog(ex);
            }
        }
    }

    public enum PhoneState
    {
        Default,
        Call,
        InCall
    }
}
