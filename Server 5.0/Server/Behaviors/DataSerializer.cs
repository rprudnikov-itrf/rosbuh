using System;
using System.IO;
using System.Data;
using System.Collections;

namespace RosService.ServiceModel
{
	class DataSerializer
	{
		static readonly DataRowVersion[] _aryVer = new DataRowVersion[2] 
				{ DataRowVersion.Original, DataRowVersion.Current };

		// Массив для перемапливания TypeCode на Type
		// TypeCode это перечисление включающее константы для базовых типов.
		static readonly Type[] _TypeMap;

		// Статический конструктор. Нужен для инициализации _TypeMap.
		static DataSerializer()
		{
			// TypeCode.DBNull и TypeCode.Empty пропущены, так как они 
			// по сути, не являются типами.
			_TypeMap = new Type[(int)TypeCode.String + 1];
			_TypeMap[(int)TypeCode.Object] = typeof(Object);
			_TypeMap[(int)TypeCode.Boolean] = typeof(Boolean);
			_TypeMap[(int)TypeCode.Char] = typeof(Char);
			_TypeMap[(int)TypeCode.SByte] = typeof(SByte);
			_TypeMap[(int)TypeCode.Byte] = typeof(Byte);
			_TypeMap[(int)TypeCode.Int16] = typeof(Int16);
			_TypeMap[(int)TypeCode.UInt16] = typeof(UInt16);
			_TypeMap[(int)TypeCode.Int32] = typeof(Int32);
			_TypeMap[(int)TypeCode.UInt32] = typeof(UInt32);
			_TypeMap[(int)TypeCode.Int64] = typeof(Int64);
			_TypeMap[(int)TypeCode.UInt64] = typeof(UInt64);
			_TypeMap[(int)TypeCode.Single] = typeof(Single);
			_TypeMap[(int)TypeCode.Double] = typeof(Double);
			_TypeMap[(int)TypeCode.Decimal] = typeof(Decimal);
			_TypeMap[(int)TypeCode.DateTime] = typeof(DateTime);
			_TypeMap[(int)TypeCode.String] = typeof(String);
		}

		///////////////////////////////////////////////////////////////////////
		// Сериализация.

		public static void SerializeDataSet(Stream stream, DataSet ds)
		{
			BinaryWriter bw = new BinaryWriter(stream);
			DataTableCollection tables = ds.Tables;

			bw.Write(ds.DataSetName);
			bw.Write(tables.Count);
			foreach(DataTable dt in tables)
			{	// Вообще-то foreach-и лучше на всякий пожарный избегать.
				// Но мне было в лом.
				SerializeDataTable(bw, dt);
			}
		}

		public static void SerializeDataTable(BinaryWriter bw, DataTable dt)
		{
			DataColumnCollection columns = dt.Columns;
			int iColCount = columns.Count;
			TypeCode[] colTypeCodes = new TypeCode[iColCount];

			bool[] aryIsNullabl = new bool[iColCount];

			// Имя таблицы
			bw.Write(dt.TableName);

			bw.Write(iColCount);
			// Получаем и записываем описание колонок.
			for(int i = 0; i < iColCount; i++)
			{
				DataColumn dc = columns[i];
				// Получаем TypeCode для типа обрабатываемой колонки.
				TypeCode tc	= Type.GetTypeCode(dc.DataType);
				// Запоминаем TypeCode колонки в соотвествующем массиве.
				colTypeCodes[i] = tc;
				bw.Write(dc.ColumnName);
				// Записываем TypeCode как Int32. Можно было бы и 
				// сэкономить 3 байта. :)
				bw.Write((Int32)tc);

				// Создаем массив информации о поддержке колонками DBNull
				aryIsNullabl[i] = dc.AllowDBNull;
			}


			// Записываем битовое поле описывающее колонки поддерживающие
			// DBNull. Если бит поднят, значит, колонка поддерживает DBNull.
			BitArray bitsNull = new BitArray(aryIsNullabl);
			byte[] byteNull = new byte[(iColCount + 7) / 8];
			bitsNull.CopyTo(byteNull, 0);
			bw.Write(byteNull);

			///////////////////////////////////////////////////////////////
			// add data

			// count rows
			bw.Write(dt.Rows.Count);

			// Записываем строки
			foreach(DataRow dr in dt.Rows)
			{
				byte verFlags = 0;
				int iVerStart;
				int iVerEnd;
				// Разбираемся, какие версии нужно писать.
				// Всего есть два варианта: Original и Current
				DataRowState state = dr.RowState;
				switch(state)
				{
						// Original + Current и они равны!
					case DataRowState.Unchanged: 
						iVerStart = 0;
						iVerEnd = 0;
						verFlags = 0;
						break;
					case DataRowState.Deleted: // Только Original
						iVerStart = 0;
						iVerEnd = 0;
						verFlags = 1;
						break;
					case DataRowState.Added: // Только Current
						iVerStart = 1;
						iVerEnd = 1;
						verFlags = 2;
						break;
						// Original + Current и они НЕ равны!
					case DataRowState.Modified:
						iVerStart = 0;
						iVerEnd = 1;
						verFlags = 3;
						break;
					default:
						throw new ApplicationException(
							"Недопустимое состояние строки: " + state.ToString());
				}

				// Пишем описание версий. Временно, так как на этом мы
				// теряем байт на строку. Куда лучше писать дополнительные два
				// бита в битовое поле DbNull (хотя это и не красиво).
				bw.Write(verFlags);

				// Записываем версии текущей строки. Всего их может быть две.
				// в принципе можно было бы для случая DataRowState.Modified
				// писать только дельту данных. Но это как-нибудь потом. :)
				for(int iVetIndex = iVerStart; iVetIndex <= iVerEnd; iVetIndex++)
				{
					DataRowVersion drv = _aryVer[iVetIndex];

					// Создаем и заполняем битовое поле. Если бит поднят,
					// значит, соответствующая колонка содержит DBNull.
					bitsNull.SetAll(false);
					for(int i = 0; i < iColCount; i++)
					{
						if(dr[i, drv] == DBNull.Value)
							bitsNull.Set(i, true);
					}
					bitsNull.CopyTo(byteNull, 0);
					// Записываем битовое поле в стрим.
					bw.Write(byteNull);

					// Перебираем колонки и пишем данные...
					for(int i = 0; i < iColCount; i++)
					{
						// Если колонка содержит DBNull, записывать ее значение 
						// ненужно.
						object data = dr[i, drv];
						if(data == DBNull.Value) // Учитываем версию!
							continue;

						// Записываем данные ячейки.
						switch(colTypeCodes[i]) 
						{	// Каждому типу соответствует переопределенная функция...
							case TypeCode.Boolean: bw.Write((Boolean)data); break;
							case TypeCode.Char: bw.Write((Char)data); break;
							case TypeCode.SByte: bw.Write((SByte)data); break;
							case TypeCode.Byte: bw.Write((Byte)data); break;
							case TypeCode.Int16: bw.Write((Int16)data); break;
							case TypeCode.UInt16: bw.Write((UInt16)data); break;
							case TypeCode.Int32: bw.Write((Int32)data); break;
							case TypeCode.UInt32: bw.Write((UInt32)data); break;
							case TypeCode.Int64: bw.Write((Int64)data); break;
							case TypeCode.UInt64: bw.Write((UInt64)data); break;
							case TypeCode.Single: bw.Write((Single)data); break;
							case TypeCode.Double: bw.Write((Double)data); break;
							case TypeCode.Decimal: bw.Write((Decimal)data); break;
							case TypeCode.DateTime:
								// Для DateTime приходится выпендриваться особым образом.
								bw.Write(((DateTime)(data)).ToFileTime());
								break;
							case TypeCode.String: bw.Write((String)data); break;
							default:
								// На всякий случай пробуем записать неопознанный тип
								// виде строки.
								bw.Write(data.ToString());
								break;
						}
					}
				}
			}
		}

		public static void SerializeDataTable(Stream stream, DataTable dt)
		{
			BinaryWriter bw = new BinaryWriter(stream);
			SerializeDataTable(bw, dt);
		}

        public static byte[] SerializeDataTable(DataTable dt)
        {
            MemoryStream stream = new MemoryStream(1024);
            BinaryWriter bw = new BinaryWriter(stream);
            SerializeDataTable(bw, dt);
            return stream.ToArray();
        }

		///////////////////////////////////////////////////////////////////////
		// Десериализация

		public static DataTable DeserializeTable(BinaryReader br)
		{
			DataColumn dc;
			DataRow dr;

			// Имя DataTable тоже передается в качестве параметра конструктора.
			DataTable dt = new DataTable(br.ReadString());
			dt.BeginLoadData();

			int iColCount = br.ReadInt32();
			dt.MinimumCapacity = iColCount;

			// В этом массив будут записаны TypeCode-ы для колонок DataTable-а.
			TypeCode[] colTypeCodes = new TypeCode[iColCount];
			// А в этот массив будут записаны типы (Type) колонок DataTable-а
			// соотвествующие TypeCode-ам.
			for(int c = 0; c < iColCount; c++)
			{
				string colName = br.ReadString();
				// Считываем TypeCode.
				TypeCode tc = (TypeCode)br.ReadInt32();
				// Помещаем TypeCode в массив для дальнейшего использования.
				colTypeCodes[c] = tc;
				// Получаем (через мап) тип соответствующий TypeCode-у.
				Type type = _TypeMap[(int)tc];
				// Создаем колонку с полученным именем и типом.
				dc = new DataColumn(colName, type);
				dt.Columns.Add(dc);
			}

				
			// Считываем список nullabl-колонок.
			int iBitLenInBytes = (iColCount + 7) / 8;
			byte[] byteNull = new byte[iBitLenInBytes];
			br.Read(byteNull, 0, iBitLenInBytes);
			BitArray bitsNull = new BitArray(byteNull);
			bitsNull.Length = iColCount;
			bool[] aryIsNullabl = new bool[iColCount];
			bitsNull.CopyTo(aryIsNullabl, 0);

			object[] ad = new object[iColCount];


			int counRows = br.ReadInt32();
			DataRowCollection rows = dt.Rows;
			for(int r = 0; r < counRows; r++)
			{
				// Читаем описание версий. Временно, так как на этом мы
				// теряем байт на строку.
				byte verFlags = br.ReadByte();
				int iVerStart;
				int iVerEnd;
				DataRowState drs;
				switch(verFlags)
				{
						// Original + Current и они равны!
					case 0: // DataRowState.Unchanged
						iVerStart = 0;
						iVerEnd = 0;
						drs = DataRowState.Unchanged;
						break;
					case 1: // DataRowState.Deleted Только Original
						iVerStart = 0;
						iVerEnd = 0;
						drs = DataRowState.Deleted;
						break;
					case 2: // DataRowState.Added Только Current
						iVerStart = 1;
						iVerEnd = 1;
						drs = DataRowState.Added;
						break;
						// Original + Current и они НЕ равны!
					case 3: // DataRowState.Modified
						iVerStart = 0;
						iVerEnd = 1;
						drs = DataRowState.Modified;
						break;
					default:
						throw new ApplicationException(
							"Недопустимое состояние строки. Сбой при загрузке.");
				}
				
				// Считываем данные.
				dr = dt.NewRow();
				rows.Add(dr);
				dr.BeginEdit();

				// Считываем версии текущей строки.
				for(int iVetIndex = iVerStart; iVetIndex <= iVerEnd; iVetIndex++)
				{
					br.Read(byteNull, 0, iBitLenInBytes);
					bitsNull = new BitArray(byteNull);

					for(int i = 0; i < iColCount; i++)
					{
						if(bitsNull.Get(i))
						{
							dr[i] = DBNull.Value;
							continue;
						}

						switch(colTypeCodes[i])
						{
							case TypeCode.Boolean: dr[i] = br.ReadBoolean(); break;
							case TypeCode.Char: dr[i] = br.ReadChar(); break;
							case TypeCode.SByte: dr[i] = br.ReadSByte(); break;
							case TypeCode.Byte: dr[i] = br.ReadByte(); break;
							case TypeCode.Int16: dr[i] = br.ReadInt16(); break;
							case TypeCode.UInt16: dr[i] = br.ReadUInt16(); break;
							case TypeCode.Int32: dr[i] = br.ReadInt32(); break;
							case TypeCode.UInt32: dr[i] = br.ReadUInt32(); break;
							case TypeCode.Int64: dr[i] = br.ReadInt64(); break;
							case TypeCode.UInt64: dr[i] = br.ReadUInt64(); break;
							case TypeCode.Single: dr[i] = br.ReadSingle(); break;
							case TypeCode.Double: dr[i] = br.ReadDouble(); break;
							case TypeCode.Decimal: dr[i] = br.ReadDecimal(); break;
							case TypeCode.DateTime:
								dr[i] = DateTime.FromFileTime(br.ReadInt64());
								break;
							case TypeCode.String: dr[i] = br.ReadString(); break;
							default:
								dr[i] = Convert.ChangeType(br.ReadString(), colTypeCodes[i]);
								break;
						}
					}
					if(iVetIndex == 0)
					{
						dr.AcceptChanges();
						if(iVerEnd > 0)
							dr.BeginEdit();
					}
				}

				if(drs == DataRowState.Deleted)
					dr.Delete();

				dr.EndEdit();
			}
			dt.EndLoadData();
			return dt;
		}

		public static DataTable DeserializeTable(Stream stream)
		{
			BinaryReader br = new BinaryReader(stream);
			return DeserializeTable(br);
		}

		public static DataSet DeserializeDataSet(Stream stream)
		{
			BinaryReader br = new BinaryReader(stream);
			// Считываем имя DataSet и создаем его...
			DataSet ds = new DataSet(br.ReadString());
			//ds.BeginInit();
			
			int counTables = br.ReadInt32();

			DataTable dt;
			for(int t = 0; t < counTables; t++)
			{
				dt = DeserializeTable(br);
				ds.Tables.Add(dt);
			}
			return ds;
		}
	}
}