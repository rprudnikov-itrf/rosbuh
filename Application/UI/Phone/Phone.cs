using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Deployment.Application;
using System.Text.RegularExpressions;

namespace RosControl.UI
{
    public class Phone : Control
    {
        public static bool PhoneAutoConnect;
        private static bool __phoneIsResources;
        private static bool __phoneIsUnload;
        private static object __phone;

        #region events
        public event RoutedEventHandler InCall
        {
            add { AddHandler(InCallEvent, value); }
            remove { RemoveHandler(InCallEvent, value); }
        }
        public static readonly RoutedEvent InCallEvent = EventManager.RegisterRoutedEvent(
            "InCall", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Phone));

        public event RoutedEventHandler InCallAccept
        {
            add { AddHandler(InCallAcceptEvent, value); }
            remove { RemoveHandler(InCallAcceptEvent, value); }
        }
        public static readonly RoutedEvent InCallAcceptEvent = EventManager.RegisterRoutedEvent(
            "InCallAccept", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Phone));


        public event RoutedEventHandler OutCall
        {
            add { AddHandler(OutCallEvent, value); }
            remove { RemoveHandler(OutCallEvent, value); }
        }
        public static readonly RoutedEvent OutCallEvent = EventManager.RegisterRoutedEvent(
            "OutCall", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Phone));


        public event RoutedEventHandler OutCallAccept
        {
            add { AddHandler(OutCallAcceptEvent, value); }
            remove { RemoveHandler(OutCallAcceptEvent, value); }
        }
        public static readonly RoutedEvent OutCallAcceptEvent = EventManager.RegisterRoutedEvent(
            "OutCallAccept", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Phone));


        public event RoutedEventHandler Calling
        {
            add { AddHandler(CallingEvent, value); }
            remove { RemoveHandler(CallingEvent, value); }
        }
        public static readonly RoutedEvent CallingEvent = EventManager.RegisterRoutedEvent(
            "Calling", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Phone));



        public event RoutedEventHandler HandUping
        {
            add { AddHandler(HandUpingEvent, value); }
            remove { RemoveHandler(HandUpingEvent, value); }
        }
        public static readonly RoutedEvent HandUpingEvent = EventManager.RegisterRoutedEvent(
            "HandUping", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Phone));
        #endregion

        public static void Shutdown()
        {
            try
            {
                if (__phone != null)
                {
                    var action = __phone.GetType().GetMethod("Disonnect");
                    if (action != null)
                        action.Invoke(__phone, null);
                }

                __phoneIsUnload = false;
                __phone = null;
            }
            catch 
            {
                //RosApplication.App.AddLog(ex);
            }
        }


        public PhoneSetupState PhoneSetupState
        {
            get { return (PhoneSetupState)GetValue(PhoneSetupStatePropertyKey.DependencyProperty); }
        }
        public static readonly DependencyPropertyKey PhoneSetupStatePropertyKey =
            DependencyProperty.RegisterReadOnly("PhoneSetupState", typeof(PhoneSetupState), typeof(Phone), new UIPropertyMetadata(PhoneSetupState.Run));

        public string Number
        {
            get
            {
                if (__phone == null)
                    return string.Empty;

                var property = __phone.GetType().GetProperty("Number");
                return property.GetValue(__phone, null) as string;
            }
        }
        public string NumberView
        {
            get
            {
                if (__phone == null)
                    return string.Empty;

                var property = __phone.GetType().GetProperty("NumberView");
                return property.GetValue(__phone, null) as string;
            }
        }
        public string DisplayName
        {
            get
            {
                if (__phone == null)
                    return string.Empty;

                var property = __phone.GetType().GetProperty("DisplayName");
                return property.GetValue(__phone, null) as string;
            }

            set
            {
                if (__phone != null)
                {
                    var property = __phone.GetType().GetProperty("DisplayName");
                    property.SetValue(__phone, value, null);
                }
            }
        }


        protected object Content
        {
            get { return (object)GetValue(ContentPropertyKey.DependencyProperty); }
        }
        protected static readonly DependencyPropertyKey ContentPropertyKey =
            DependencyProperty.RegisterReadOnly("Content", typeof(object), typeof(Phone), new UIPropertyMetadata(null));




        public int ProgressPercentage
        {
            get { return (int)GetValue(ProgressPercentageProperty.DependencyProperty); }
        }
        public static readonly DependencyPropertyKey ProgressPercentageProperty =
            DependencyProperty.RegisterReadOnly("ProgressPercentage", typeof(int), typeof(Phone), new UIPropertyMetadata(0));




        static Phone()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Phone), new FrameworkPropertyMetadata(typeof(Phone)));
            KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(Phone), new FrameworkPropertyMetadata(KeyboardNavigationMode.Once));
            KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(typeof(Phone), new FrameworkPropertyMetadata(KeyboardNavigationMode.Contained));

            CommandManager.RegisterClassCommandBinding(typeof(Phone), new CommandBinding(ApplicationCommands.Open, Call_Execute));
        }

        private static void Call_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (sender is Phone)
                    ((Phone)sender).Call(Convert.ToString(e.Parameter ?? ((Phone)sender).Number));
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            try
            {
                if (System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed)
                {
                    var deployment = System.Deployment.Application.ApplicationDeployment.CurrentDeployment;
                    if (deployment != null && deployment.IsFileGroupDownloaded("VoIPSDK"))
                    {
                        CreatInstanse();
                    }
                    else
                    {
                        SetLoaded();
                    }
                }
                else
                {
                    CreatInstanse();
                }
            }
            catch
            {
                SetLoaded();
            }
        }

        private void SetLoaded()
        {
            SetValue(PhoneSetupStatePropertyKey, PhoneSetupState.Setup);
        }

        private void CreatInstanse()
        {
            if (__phone == null)
            {
                if (!__phoneIsResources)
                {
                    var resource = new Uri("/RosApplication;Component/UI/Phone/Themes/PhoneControl.xaml", UriKind.RelativeOrAbsolute);
                    Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = resource });
                    __phoneIsResources = true;
                }
                __phone = Activator.CreateInstance(Type.GetType("RosControl.UI.PhoneControl"));
            }

            if (__phone != null)
            {
                //подключится ещё раз если телефон был выгружен
                if (__phoneIsUnload && PhoneAutoConnect)
                {
                    __phoneIsUnload = false;
                    Connect();
                }

                SetValue(ContentPropertyKey, __phone);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var btnLoad = GetTemplateChild("PART_btnLoad") as Button;
            if (btnLoad != null)
            {
                btnLoad.Click += new RoutedEventHandler(btnLoad_Click);
            }

            var btnReboot = GetTemplateChild("PART_btnReboot") as Button;
            if (btnReboot != null)
            {
                btnReboot.Click += new RoutedEventHandler(btnReboot_Click);
            }           

            BindHostContanier();
        }

        private void btnReboot_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            SetValue(PhoneSetupStatePropertyKey, PhoneSetupState.Download);

            if (System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed)
            {
                var deployment = System.Deployment.Application.ApplicationDeployment.CurrentDeployment;
                if (!deployment.IsFileGroupDownloaded("VoIPSDK"))
                {
                    deployment.DownloadFileGroupCompleted += delegate
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            SetValue(PhoneSetupStatePropertyKey, PhoneSetupState.Reboot);
                        });
                    };
                    deployment.DownloadFileGroupProgressChanged += delegate(object _sender, DeploymentProgressChangedEventArgs _e)
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                        {
                            SetValue(ProgressPercentageProperty, _e.ProgressPercentage);
                        });
                    };
                    deployment.DownloadFileGroupAsync("VoIPSDK");
                }
            }
        }

        public void Call(string number)
        {
            if (__phone != null)
            {
                var action = __phone.GetType().GetMethod("Call", new Type[] { typeof(string) });
                if (action != null)
                    action.Invoke(__phone, new object[] { number });
            }
        }
        public void Connect()
        {
            try
            {
                if (__phone != null)
                {
                    var action = __phone.GetType().GetMethod("Connect");
                    if (action != null)
                        action.Invoke(__phone, null);
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
                if (__phone != null)
                {
                    var action = __phone.GetType().GetMethod("Disonnect");
                    if (action != null)
                        action.Invoke(__phone, null);
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        private FrameworkElement __HostContanier;
        private void BindHostContanier()
        {
            try
            {
                //отключить телефон при закрытии вкладки
                if (__phone != null)
                {
                    var DocumentsTabControl = RosControl.Helper.FindParentLogicalControl<RosControl.UI.DocumentsTabControl>(this);
                    if (DocumentsTabControl != null)
                    {
                        DocumentsTabControl.TabItemClosing += new EventHandler<TabItemCancelEventArgs>(DocumentsTabControl_TabItemClosing);
                        __HostContanier = DocumentsTabControl;
                        return;
                    }

                    var HostContanier = RosControl.Helper.FindParentLogicalControl<System.Windows.Window>(this);
                    if (HostContanier != null)
                    {
                        HostContanier.Closing += new System.ComponentModel.CancelEventHandler(HostContanier_Closing);
                        __HostContanier = HostContanier;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        private void HostContanier_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                var HostContanier = __HostContanier as System.Windows.Window;
                if (HostContanier != null)
                    HostContanier.Closing -= new System.ComponentModel.CancelEventHandler(HostContanier_Closing);

                __HostContanier = null;
                __phoneIsUnload = true;

                Disonnect();
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        private void DocumentsTabControl_TabItemClosing(object sender, TabItemCancelEventArgs e)
        {
            try
            {
                var DocumentsTabControl = __HostContanier as DocumentsTabControl;
                var currentTab = Helper.FindParentLogicalControl<TabItem>(this);
                if (DocumentsTabControl != null && Equals(currentTab, sender))
                {
                    DocumentsTabControl.TabItemClosing -= new EventHandler<TabItemCancelEventArgs>(DocumentsTabControl_TabItemClosing);

                    __HostContanier = null;
                    __phoneIsUnload = true;

                    Disonnect();
                }
            }
            catch (Exception ex)
            {
                RosApplication.App.AddLog(ex);
            }
        }

        public static string FormatNumber(string phone, bool autocorrect = true)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            var Представление = string.Empty;

            phone = new Regex(@"[^\d]", RegexOptions.IgnoreCase).Replace(phone, "");

            // пропускаем телефон через систему смены кодов 
            if (autocorrect && phone.Length == 10)
                phone = "8" + phone;

            /* Заполняем Представление */
            //if (Телефон.Length == 4)
            //    return Convert.ToDecimal(Телефон).ToString("# (###)");
            if (phone.Length == 5)
                return Convert.ToDecimal(phone).ToString("# (###) #");
            else if (phone.Length == 6)
                return Convert.ToDecimal(phone).ToString("# (###) ##");
            else if (phone.Length == 7)
                return Convert.ToDecimal(phone).ToString("# (###) ###");
            else if (phone.Length == 8)
                return Convert.ToDecimal(phone).ToString("# (###) ###-#");
            else if (phone.Length == 9)
                return Convert.ToDecimal(phone).ToString("# (###) ###-##");
            else if (phone.Length == 10)
                return Convert.ToDecimal(phone).ToString("# (###) ###-##-#");
            else if (phone.Length == 11)
                return Convert.ToDecimal(phone).ToString("# (###) ###-##-##");
            else
                return phone;
        }
    }

    public enum PhoneSetupState
    {
        Run,
        Setup,
        Download,
        Reboot
    }
}
