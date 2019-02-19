using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data;
using System.Windows.Threading;

namespace RosControl.UI
{
    public class RecordsFrame : Control
    {
        RosControl.UI.DataGrid Table1;
        string db;



        public string Phone
        {
            get { return (string)GetValue(PhoneProperty); }
            set { SetValue(PhoneProperty, value); }
        }
        public static readonly DependencyProperty PhoneProperty =
            DependencyProperty.Register("Phone", typeof(string), typeof(RecordsFrame), new UIPropertyMetadata(null));


        public int Perriod
        {
            get { return (int)GetValue(PerriodProperty); }
            set { SetValue(PerriodProperty, value); }
        }
        public static readonly DependencyProperty PerriodProperty =
            DependencyProperty.Register("Perriod", typeof(int), typeof(RecordsFrame), new UIPropertyMetadata(0));







        public RecordsUser SelectedUser
        {
            get { return (RecordsUser)GetValue(SelectedUserProperty); }
            set { SetValue(SelectedUserProperty, value); }
        }
        public static readonly DependencyProperty SelectedUserProperty =
            DependencyProperty.Register("SelectedUser", typeof(RecordsUser), typeof(RecordsFrame), new UIPropertyMetadata(null));



        public IEnumerable<RecordsUser> Users
        {
            get { return (IEnumerable<RecordsUser>)GetValue(UsersProperty); }
            set { SetValue(UsersProperty, value); }
        }
        public static readonly DependencyProperty UsersProperty =
            DependencyProperty.Register("Users", typeof(IEnumerable<RecordsUser>), typeof(RecordsFrame), new UIPropertyMetadata(null));


        public DateTime? DateStart
        {
            get { return (DateTime?)GetValue(DateStartProperty); }
            set { SetValue(DateStartProperty, value); }
        }
        public static readonly DependencyProperty DateStartProperty =
            DependencyProperty.Register("DateStart", typeof(DateTime?), typeof(RecordsFrame), new UIPropertyMetadata(DateTime.Today.AddDays(-3)));


        public DateTime? DateEnd
        {
            get { return (DateTime?)GetValue(DateEndProperty); }
            set { SetValue(DateEndProperty, value); }
        }
        public static readonly DependencyProperty DateEndProperty =
            DependencyProperty.Register("DateEnd", typeof(DateTime?), typeof(RecordsFrame), new UIPropertyMetadata(DateTime.Today.AddDays(1)));



        


        static RecordsFrame()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RecordsFrame), new FrameworkPropertyMetadata(typeof(RecordsFrame)));
        }

        public RecordsFrame()
        {
            db = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(RosService.Client.Domain)).Replace("+", "%2b").Replace("=","%3d"); 
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var PART_Form = GetTemplateChild("PART_Form") as RosControl.UI.Window;
            if (PART_Form != null)
            {
                PART_Form.PreviewKeyDown += new KeyEventHandler(PART_Form_PreviewKeyDown);
            }

            Table1 = GetTemplateChild("Table1") as RosControl.UI.DataGrid;
            if (Table1 != null)
            {
                Table1._isFirstLoaded = true;
                Table1.Searched += new EventHandler<DataGridArgs>(Table1_Searched);
            }

            var PART_Filter = GetTemplateChild("PART_Filter") as TextBox;
            if (PART_Filter != null)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                {
                    PART_Filter.Focus();
                });
            }

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var query = new RosService.Data.Query("[Типы=Пользователь;Колонки=(НазваниеОбъекта,SipНомер);Сортировки=(НазваниеОбъекта,Asc);Условия=(РазрешитьВход,True,Равно)(SipНомер, ,НеРавно)]");
                        var items = new List<RecordsUser>();
                        items.Add(new RecordsUser()
                        {
                            sip = "все"
                        });
                        foreach (var item in client.Архив.Поиск(query).AsEnumerable())
                        {
                            items.Add(new RecordsUser()
                            {
                                id = item.Convert<decimal>("id_node"),
                                name = item.Convert<string>("НазваниеОбъекта"),
                                sip = item.Convert<string>("SipНомер"),
                            });
                        }

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            Users = items;
                            var selectedUser = items.FirstOrDefault(p => p.id == RosService.Client.User.id_node);
                            if (selectedUser != null && (Phone == null || Phone.Count() == 0))
                                SelectedUser = selectedUser;
                            else
                                SelectedUser = items.First();

                            //Table1.Поиск();
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                }
            });
        }

        void Table1_Searched(object sender, DataGridArgs e)
        {
            Table1.SelectedItem = null;
            e.query.СтрокаЗапрос = string.Format("http://api.itrf.ru/voip/records/?db={2}&sip={1}&perrid={0}&format=xml",
                Perriod,
                SelectedUser != null && SelectedUser.id > 0 ? SelectedUser.sip : "",
                db);

            if (Perriod == 7)
            {
                if (!DateStart.HasValue)
                    DateStart = DateTime.Today.AddDays(3);

                if (!DateEnd.HasValue)
                    DateEnd = DateTime.Today.AddDays(1);

                e.query.СтрокаЗапрос += "&start=" + System.Xml.XmlConvert.ToString(DateStart.Value, System.Xml.XmlDateTimeSerializationMode.Unspecified);
                e.query.СтрокаЗапрос += "&end=" + System.Xml.XmlConvert.ToString(DateEnd.Value, System.Xml.XmlDateTimeSerializationMode.Unspecified);
            }

            if (!string.IsNullOrEmpty(Phone))
            {
                e.query.СтрокаЗапрос += "&phone=" + Phone;
            }
        }

        void PART_Form_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5 && Table1 != null)
                Table1.Поиск();
        }
    }

    public class RecordsUser
    {
        public decimal id { get; set; }
        public string name { get; set; }
        public string sip { get; set; }
    }
}
