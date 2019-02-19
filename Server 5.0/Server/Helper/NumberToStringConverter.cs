using System;
using System.Text;

namespace RosService.Helper
{
    /// <summary>
    /// ����� �������������� ��� ������������� ���� �� ������ ��������
    /// </summary>
    class TillThousandToStringConverter
    {
        /// <summary>
        /// �����
        /// </summary>
        private static string[] _hundreds =
        {
            string.Empty, "��� ", "������ ", "������ ", "��������� ",
            "������� ", "�������� ", "������� ", "��������� ", "��������� "
        };
        
        /// <summary>
        /// �������
        /// </summary>
        private static string[] _tens =
        {
            string.Empty, "������ ", "�������� ", "�������� ", "����� ", "��������� ",
            "���������� ", "��������� ", "����������� ", "��������� "
        };
        
        /// <summary>
        /// ������� � ������� ����
        /// </summary>
        private static string[] _maleUnits =
        {
            "", "���� ", "��� ", "��� ", "������ ", "���� ", "����� ",
            "���� ", "������ ", "������ ", "������ ", "����������� ",
            "���������� ", "���������� ", "������������ ", "���������� ",
            "����������� ", "���������� ", "������������ ", "������������ "
        };
        
        /// <summary>
        /// ������� � ������� ����
        /// </summary>
        private static string[] _femaleUnits = 
        {
            "", "���� ", "��� ", "��� ", "������ ", "���� ", "����� ",
            "���� ", "������ ", "������ ", "������ ", "����������� ",
            "���������� ", "���������� ", "������������ ", "���������� ",
            "����������� ", "���������� ", "������������ ", "������������ "
        };
        
        /// <summary>
        /// ����� ������������ ����� �� ������ ������ ��������
        /// </summary>
        /// <param name="number">����� ��� �������������</param>
        /// <param name="isMale">���� ������������ ��� �����</param>
        /// <returns>����� ��������</returns>
        public static string Convert(ushort number, bool isMale)
        {
            StringBuilder s = new StringBuilder();
            
            // �������� �����
            s.Append( _hundreds[number / 100] );
            
            // �������� ������� � �������
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
    /// ����� ��� ������������� ���������
    /// </summary>
    public class NumberDeclension
    {
        
        /// <summary>
        /// �����������
        /// </summary>
        public NumberDeclension() : this(true){}
        
        /// <summary>
        /// �����������
        /// </summary>
        /// <param name="isMale">��� �����</param>
        public NumberDeclension(bool isMale) : this(isMale, string.Empty, string.Empty, string.Empty){}
        
        /// <summary>
        /// �����������
        /// </summary>
        /// <param name="isMale">��� �����</param>
        /// <param name="one">��������� ��� �������</param>
        /// <param name="two">��������� �� ���� �� �������</param>
        /// <param name="five">��������� ������ ����</param>
        public NumberDeclension(bool isMale, string one, string two, string five)
        {
            IsMale = isMale;
            One = one;
            Two = two;
            Five = five;
        }
        
        /// <summary>
        /// ���������� ���������
        /// </summary>
        /// <param name="n">�����</param>
        /// <returns>���������</returns>
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
        /// ���� ������������ ������� ��� ��� ��� � �����
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
        /// ��������� ��� �������
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
        /// ��������� ��� ���, ���� � �������
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
        /// ��������� ��� ����� ������ �������
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
        /// ���� ������������ ������� ��� ��� ��� � �����
        /// </summary>
        private bool _isMale;
        /// <summary>
        /// ��������� ��� �������
        /// </summary>
        private string _one;
        /// <summary>
        /// ��������� ��� ���, ���� � �������
        /// </summary>
        private string _two;
        /// <summary>
        /// ��������� ��� ����� ������ �������
        /// </summary>
        private string _five;

    }
    
    /// <summary>
    /// ����� �������������� ����� ������ ��������
    /// </summary>
    public class NumberToStringConverter
    {
        private static readonly NumberDeclension _thousand = new NumberDeclension(false, "������ ", "������ ", "����� ");
        private static readonly NumberDeclension _million = new NumberDeclension(true, "������� ", "�������� ", "��������� ");
        private static readonly NumberDeclension _billion = new NumberDeclension(true, "�������� ", "��������� ", "���������� ");
        private static readonly NumberDeclension _trillion = new NumberDeclension(true, "�������� ", "��������� ", "���������� ");
        private static readonly NumberDeclension _quadrillion = new NumberDeclension(true, "��������� ", "���������� ", "����������� ");
        
        /// <summary>
        /// ������������ ����� ������ ��������
        /// </summary>
        /// <param name="n">�����</param>
        /// <returns>����� ��������</returns>
        public static string Convert(System.Int64 n)
        {
            return Convert(n, new NumberDeclension(true) );
        }
        
        /// <summary>
        /// ������������ ����� ������ ��������
        /// </summary>
        /// <param name="n">�����</param>
        /// <param name="d">�������������� �����������</param>
        /// <returns>����� ��������</returns>
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
                    "���� " + 
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
                s.Insert(0, "����� ");
                
            return s.ToString();
        }
    }
    
    /// <summary>
    /// ����� ��������������� ��� ������������� ���� � ������ ������� ������ ��������
    /// </summary>
    public class CurrencySumsConverter
    {
        public CurrencySumsConverter(NumberDeclension majorCurrency, NumberDeclension minorCurrency)
        {
            _majorCurrency = majorCurrency;
            _minorCurrency = minorCurrency;
        }
        
        /// <summary>
        /// ������������ ����� � ������ ��������
        /// </summary>
        /// <param name="number">�����</param>
        /// <returns>����� ��������</returns>
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
        /// ��������������� �������� ����
        /// </summary>
        public static CurrencySumsConverter RublesConverter
        {
            get
            {
                return new CurrencySumsConverter(
                    new NumberDeclension(true, "�����", "�����", "������"),
                    new NumberDeclension(false, "�������", "�������", "������"));
            }
        }
        
        /// <summary>
        /// ��������������� ���� � ���������� ������
        /// </summary>
        public static CurrencySumsConverter DollarsConverter
        {
            get
            {
                return new CurrencySumsConverter(
                    new NumberDeclension(true, "������", "�������", "��������"),
                    //new NumberDeclension(false, "����", "�����", "������"));
                    new NumberDeclension(true, "����", "�����", "������"));
            }
        }
        
        /// <summary>
        /// ��������������� ���� � ����
        /// </summary>
        public static CurrencySumsConverter EuroConverter
        {
            get
            {
                return new CurrencySumsConverter(
                    new NumberDeclension(true, "����", "����", "����"),
                    new NumberDeclension(true, "��������", "���������", "����������"));
            }
        }
        
        /// <summary>
        /// ��������������� ����� � ����� ��������
        /// </summary>
        /// <param name="n">�����</param>
        /// <param name="majorCurrency">�������� ������</param>
        /// <param name="minorCurrency">������� ������ (������� �� ��������� � ������)</param>
        /// <returns></returns>
        public static string Convert(decimal n, NumberDeclension majorCurrency, NumberDeclension minorCurrency)
        {
            return new CurrencySumsConverter(majorCurrency, minorCurrency).Convert(n);
        }
        
        
        private NumberDeclension _majorCurrency;
        private NumberDeclension _minorCurrency;
    }
}