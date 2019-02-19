using System;
using System.Text;

namespace RosService.Helper
{
    /// <summary>
    /// Класс предназначеный для представления сумм до тысячи прописью
    /// </summary>
    class TillThousandToStringConverter
    {
        /// <summary>
        /// Сотни
        /// </summary>
        private static string[] _hundreds =
        {
            string.Empty, "сто ", "двести ", "триста ", "четыреста ",
            "пятьсот ", "шестьсот ", "семьсот ", "восемьсот ", "девятьсот "
        };
        
        /// <summary>
        /// Десятки
        /// </summary>
        private static string[] _tens =
        {
            string.Empty, "десять ", "двадцать ", "тридцать ", "сорок ", "пятьдесят ",
            "шестьдесят ", "семьдесят ", "восемьдесят ", "девяносто "
        };
        
        /// <summary>
        /// Единицы в мужском роде
        /// </summary>
        private static string[] _maleUnits =
        {
            "", "один ", "два ", "три ", "четыре ", "пять ", "шесть ",
            "семь ", "восемь ", "девять ", "десять ", "одиннадцать ",
            "двенадцать ", "тринадцать ", "четырнадцать ", "пятнадцать ",
            "шестнадцать ", "семнадцать ", "восемнадцать ", "девятнадцать "
        };
        
        /// <summary>
        /// Единицы в женском роде
        /// </summary>
        private static string[] _femaleUnits = 
        {
            "", "одна ", "две ", "три ", "четыре ", "пять ", "шесть ",
            "семь ", "восемь ", "девять ", "десять ", "одиннадцать ",
            "двенадцать ", "тринадцать ", "четырнадцать ", "пятнадцать ",
            "шестнадцать ", "семнадцать ", "восемнадцать ", "девятнадцать "
        };
        
        /// <summary>
        /// Метод представляет числа до тысячи суммой прописью
        /// </summary>
        /// <param name="number">Число для представления</param>
        /// <param name="isMale">Флаг показывающий род числа</param>
        /// <returns>Сумма прописью</returns>
        public static string Convert(ushort number, bool isMale)
        {
            StringBuilder s = new StringBuilder();
            
            // Печатаем сотни
            s.Append( _hundreds[number / 100] );
            
            // Печатаем десятки и единицы
            string[] units = null;
            if (isMale)
                units = _maleUnits;
            else
                units = _femaleUnits;

            ushort numberTillHundred = (ushort)(number % 100);

            if ( numberTillHundred >= 20 ) 
            {
                s.Append( _tens[ numberTillHundred / 10 ] );
                if ((numberTillHundred % 10) != 0)
                    s.Append( units[ numberTillHundred % 10 ] );
            }
            else
                s.Append( units[ numberTillHundred % 20 ] );
            
            
            return s.ToString();
        }
    }
    
    /// <summary>
    /// Класс для представления склонение
    /// </summary>
    public class NumberDeclension
    {
        
        /// <summary>
        /// Конструктор
        /// </summary>
        public NumberDeclension() : this(true){}
        
        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="isMale">Род числа</param>
        public NumberDeclension(bool isMale) : this(isMale, string.Empty, string.Empty, string.Empty){}
        
        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="isMale">Род числа</param>
        /// <param name="one">Склонение для единицы</param>
        /// <param name="two">Склонение от двух до четырех</param>
        /// <param name="five">Склонение больше пяти</param>
        public NumberDeclension(bool isMale, string one, string two, string five)
        {
            IsMale = isMale;
            One = one;
            Two = two;
            Five = five;
        }
        
        /// <summary>
        /// Возвращает склонение
        /// </summary>
        /// <param name="n">Число</param>
        /// <returns>Склонение</returns>
        public string GetDeclension(ushort n)
        {
            switch( ((n % 100 > 20) ? n % 10 : n % 20) )
            {
                case 1: 
                    return One;
                case 2:
                case 3:
                case 4:
                    return Two;
                default:
                    return Five;
            }
        }
        
        
        /// <summary>
        /// Флаг показывающий мужской или нет род у числа
        /// </summary>
        public bool IsMale
        {
            get
            {
                return _isMale;
            }
            set
            {
                _isMale = value;
            }
        }

        /// <summary>
        /// Склонение для единицы
        /// </summary>
        public string One
        {
            get
            {
                return _one;
            }
            set
            {
                
                _one = value.TrimEnd() + " ";
            }
        }

        /// <summary>
        /// Склонение для пар, трех и четырех
        /// </summary>
        public string Two
        {
            get
            {
                return _two;
            }
            set
            {
                _two = value.TrimEnd() + " ";
            }
        }

        /// <summary>
        /// Склонение для чисел больше четырех
        /// </summary>
        public string Five
        {
            get
            {
                return _five;
            }
            set
            {
                _five = value.TrimEnd() + " ";
            }
        }
        
        
        
        
        /// <summary>
        /// Флаг показывающий мужской или нет род у числа
        /// </summary>
        private bool _isMale;
        /// <summary>
        /// Склонение для единицы
        /// </summary>
        private string _one;
        /// <summary>
        /// Склонение для пар, трех и четырех
        /// </summary>
        private string _two;
        /// <summary>
        /// Склонение для чисел больше четырех
        /// </summary>
        private string _five;

    }
    
    /// <summary>
    /// Класс представляющий числа суммой прописью
    /// </summary>
    public class NumberToStringConverter
    {
        private static readonly NumberDeclension _thousand = new NumberDeclension(false, "тысяча ", "тысячи ", "тысяч ");
        private static readonly NumberDeclension _million = new NumberDeclension(true, "миллион ", "миллиона ", "миллионов ");
        private static readonly NumberDeclension _billion = new NumberDeclension(true, "миллиард ", "миллиарда ", "миллиардов ");
        private static readonly NumberDeclension _trillion = new NumberDeclension(true, "триллион ", "триллиона ", "триллионов ");
        private static readonly NumberDeclension _quadrillion = new NumberDeclension(true, "триллиард ", "триллиарда ", "триллиардов ");
        
        /// <summary>
        /// Представляет число суммой прописью
        /// </summary>
        /// <param name="n">Число</param>
        /// <returns>Сумма прописью</returns>
        public static string Convert(System.Int64 n)
        {
            return Convert(n, new NumberDeclension(true) );
        }
        
        /// <summary>
        /// Представляет число суммой прописью
        /// </summary>
        /// <param name="n">Число</param>
        /// <param name="d">Количественные размерности</param>
        /// <returns>Сумма прописью</returns>
        public static string Convert(System.Int64 n, NumberDeclension d)
        {
            NumberDeclension[] declensions = 
                new NumberDeclension[] {
                    d,
                    _thousand,
                    _million,
                    _billion,
                    _trillion,
                    _quadrillion
                };

            if (n == Int64.MinValue)
                throw new ArgumentException();
                
            System.Int64 number = n > 0 ? n : -n;
            StringBuilder s = new StringBuilder();

            if (number == 0)
                s.Insert(0, 
                    "ноль " + 
                    d.GetDeclension((ushort)(number)) );
            else
                foreach(NumberDeclension declension in declensions)
                {
                    s.Insert(0, 
                        TillThousandToStringConverter.Convert( (ushort)(number % 1000), declension.IsMale ) + 
                        declension.GetDeclension((ushort)(number % 1000)) );
                    
                    
                        
                    number /= 1000;
                    
                    if (number == 0)
                        break;
                }
            
            if (n < 0)
                s.Insert(0, "минус ");
                
            return s.ToString();
        }
    }
    
    /// <summary>
    /// Класс предназначенный для предстваления сумм в разных валютах суммой прописью
    /// </summary>
    public class CurrencySumsConverter
    {
        public CurrencySumsConverter(NumberDeclension majorCurrency, NumberDeclension minorCurrency)
        {
            _majorCurrency = majorCurrency;
            _minorCurrency = minorCurrency;
        }
        
        /// <summary>
        /// Представляет сумму в валюте прописью
        /// </summary>
        /// <param name="number">Сумма</param>
        /// <returns>Сумма прописью</returns>
        public string Convert(decimal number)
        {
            if (number == decimal.MinValue)
                throw new ArgumentException();
                
            Int64 major = (Int64)number;    
            Int64 minor = (Int64)((number - major + 0.005m) * 100);
            
            string str = NumberToStringConverter.Convert(major, _majorCurrency);
            return string.Format( "{0}{1}{2}", 
                str.Substring(0,1).ToUpper(),
                str.Substring(1),
                NumberToStringConverter.Convert(minor, _minorCurrency));
        }

        public string ConvertShort(decimal number)
        {
            if (number == decimal.MinValue)
                throw new ArgumentException();

            Int64 major = (Int64)number;
            Int64 minor = (Int64)((number - major + 0.005m) * 100);

            string str = NumberToStringConverter.Convert(major, _majorCurrency);
            return string.Format("{0}{1}",
                str.Substring(0, 1).ToUpper(),
                str.Substring(1));
        }
        
        /// <summary>
        /// Преобразователь рублевых сумм
        /// </summary>
        public static CurrencySumsConverter RublesConverter
        {
            get
            {
                return new CurrencySumsConverter(
                    new NumberDeclension(true, "рубль", "рубля", "рублей"),
                    new NumberDeclension(false, "копейка", "копейки", "копеек"));
            }
        }
        
        /// <summary>
        /// Преобразователь сумм в долларовой валюте
        /// </summary>
        public static CurrencySumsConverter DollarsConverter
        {
            get
            {
                return new CurrencySumsConverter(
                    new NumberDeclension(true, "доллар", "доллара", "долларов"),
                    //new NumberDeclension(false, "цент", "цента", "центов"));
                    new NumberDeclension(true, "цент", "цента", "центов"));
            }
        }
        
        /// <summary>
        /// Преобразователь сумм в евро
        /// </summary>
        public static CurrencySumsConverter EuroConverter
        {
            get
            {
                return new CurrencySumsConverter(
                    new NumberDeclension(true, "евро", "евро", "евро"),
                    new NumberDeclension(true, "евроцент", "евроцента", "евроцентов"));
            }
        }
        
        /// <summary>
        /// Преобразовывает число с сумму прописью
        /// </summary>
        /// <param name="n">Число</param>
        /// <param name="majorCurrency">Основная валюта</param>
        /// <param name="minorCurrency">Младшая валюта (копейки по отношению к рублям)</param>
        /// <returns></returns>
        public static string Convert(decimal n, NumberDeclension majorCurrency, NumberDeclension minorCurrency)
        {
            return new CurrencySumsConverter(majorCurrency, minorCurrency).Convert(n);
        }
        
        
        private NumberDeclension _majorCurrency;
        private NumberDeclension _minorCurrency;
    }
}