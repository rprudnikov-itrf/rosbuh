using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RosService.Configuration;
using System.Data;
using System.ComponentModel;

namespace RosService.Users
{
    public class НаборПраваПользователя : INotifyPropertyChanged
    {
        private ПраваПользователя _НаборПравДоступа;
        public ПраваПользователя НаборПравДоступа 
        {
            get
            {
                return _НаборПравДоступа;
            }
            set
            {
                if (_НаборПравДоступа != value)
                {
                    _НаборПравДоступа = value;

                    NotifyPropertyChanged("ДобавлениеРазделов");
                    NotifyPropertyChanged("УдалениеРазделов");
                    NotifyPropertyChanged("РедактированиеРазделов");
                    NotifyPropertyChanged("ПоказатьВсеДерево");

                    NotifyPropertyChanged("ПоказатьОбъектыСозданныеПользователем");
                    NotifyPropertyChanged("ПоказатьОбъектыПодструктуры");
                    NotifyPropertyChanged("УправлениеПользователями");
                    NotifyPropertyChanged("ПоказатьОбъектыПоАтрибуту");

                    NotifyPropertyChanged("СкрытьРекламу");
                    NotifyPropertyChanged("ЗапретитьРаботуСПочтой");
                    NotifyPropertyChanged("ЗапретитьРаботуСЗадачами");
                    NotifyPropertyChanged("ЗапретитьПоиск");
                    NotifyPropertyChanged("ЗапретитьРасширенныйПоиск");
                    //NotifyPropertyChanged("ЗапретитьУдаленныеПодключения");
                }
            }
        }
        public bool ДобавлениеРазделов
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ДобавлениеРазделов) == ПраваПользователя.ДобавлениеРазделов;
            }
        }
        public bool УдалениеРазделов
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.УдалениеРазделов) == ПраваПользователя.УдалениеРазделов;
            }
        }
        public bool РедактированиеРазделов
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.РедактированиеРазделов) == ПраваПользователя.РедактированиеРазделов;
            }
        }
        public bool ПоказатьВсеДерево
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ПоказатьВсеДерево) == ПраваПользователя.ПоказатьВсеДерево;
            }
        }
        public bool ПоказатьОбъектыСозданныеПользователем
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ПоказатьОбъектыСозданныеПользователем) == ПраваПользователя.ПоказатьОбъектыСозданныеПользователем;
            }
        }
        public bool ПоказатьОбъектыПодструктуры
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ПоказатьОбъектыПодструктуры) == ПраваПользователя.ПоказатьОбъектыПодструктуры;
            }
        }
        public bool УправлениеПользователями
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.УправлениеПользователями) == ПраваПользователя.УправлениеПользователями;
            }
        }
        public bool ПоказатьОбъектыПоАтрибуту
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ПоказатьОбъектыПоАтрибуту) == ПраваПользователя.ПоказатьОбъектыПоАтрибуту;
            }
        }



        public bool СкрытьРекламу
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.СкрытьРекламу) == ПраваПользователя.СкрытьРекламу;
            }
        }
        public bool ЗапретитьРаботуСПочтой
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ЗапретитьРаботуСПочтой) == ПраваПользователя.ЗапретитьРаботуСПочтой;
            }
        }
        public bool ЗапретитьРаботуСЗадачами
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ЗапретитьРаботуСЗадачами) == ПраваПользователя.ЗапретитьРаботуСЗадачами;
            }
        }
        public bool ЗапретитьПоиск
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ЗапретитьПоиск) == ПраваПользователя.ЗапретитьПоиск;
            }
        }
        public bool ЗапретитьРасширенныйПоиск
        {
            get
            {
                return (НаборПравДоступа & ПраваПользователя.ЗапретитьРасширенныйПоиск) == ПраваПользователя.ЗапретитьРасширенныйПоиск;
            }
        }
        //public bool ЗапретитьУдаленныеПодключения
        //{
        //    get
        //    {
        //        return (НаборПравДоступа & ПраваПользователя.ЗапретитьУдаленныеПодключения) == ПраваПользователя.ЗапретитьУдаленныеПодключения;
        //    }
        //}


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(property));
            }
        }
        #endregion
    };

    public class UserClient
    {
        public decimal id_node { get; set; }

        public string Логин
        {
            get
            {
                return Client.UserName;
            }
        }
        public string Интерфейс { get; set; }
        public string Группа { get; set; }
        public string Тип { get; set; }
        public decimal ГруппаРаздел { get; set; }
        public НаборПраваПользователя Права { get; set; }
        public string[] Роли { get; set; }
        //public decimal МестоПоиска { get; set; }
        //public string ПоисковыйАтрибут { get; set; }

        public bool IsSupport
        {
            get
            {
                return string.Equals("Техподдержка", Client.UserName, StringComparison.CurrentCultureIgnoreCase)
                    || string.Equals("ЛокальнаяСистема", Client.UserName, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        public bool Роль(string value)
        {
            return Роли.Count(p => p.Equals(value)) > 0;
        }
    }
}
