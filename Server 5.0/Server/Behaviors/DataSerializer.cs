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

		// ������ ��� �������������� TypeCode �� Type
		// TypeCode ��� ������������ ���������� ��������� ��� ������� �����.
		static readonly Type[] _TypeMap;

		// ����������� �����������. ����� ��� ������������� _TypeMap.
		static DataSerializer()
		{
			// TypeCode.DBNull � TypeCode.Empty ���������, ��� ��� ��� 
			// �� ����, �� �������� ������.
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
		// ������������.

		public static void SerializeDataSet(Stream stream, DataSet ds)
		{
			BinaryWriter bw = new BinaryWriter(stream);
			DataTableCollection tables = ds.Tables;

			bw.Write(ds.DataSetName);
			bw.Write(tables.Count);
			foreach(DataTable dt in tables)
			{	// ������-�� foreach-� ����� �� ������ �������� ��������.
				// �� ��� ���� � ���.
				SerializeDataTable(bw, dt);
			}
		}

		public static void SerializeDataTable(BinaryWriter bw, DataTable dt)
		{
			DataColumnCollection columns = dt.Columns;
			int iColCount = columns.Count;
			TypeCode[] colTypeCodes = new TypeCode[iColCount];

			bool[] aryIsNullabl = new bool[iColCount];

			// ��� �������
			bw.Write(dt.TableName);

			bw.Write(iColCount);
			// �������� � ���������� �������� �������.
			for(int i = 0; i < iColCount; i++)
			{
				DataColumn dc = columns[i];
				// �������� TypeCode ��� ���� �������������� �������.
				TypeCode tc	= Type.GetTypeCode(dc.DataType);
				// ���������� TypeCode ������� � �������������� �������.
				colTypeCodes[i] = tc;
				bw.Write(dc.ColumnName);
				// ���������� TypeCode ��� Int32. ����� ���� �� � 
				// ���������� 3 �����. :)
				bw.Write((Int32)tc);

				// ������� ������ ���������� � ��������� ��������� DBNull
				aryIsNullabl[i] = dc.AllowDBNull;
			}


			// ���������� ������� ���� ����������� ������� ��������������
			// DBNull. ���� ��� ������, ������, ������� ������������ DBNull.
			BitArray bitsNull = new BitArray(aryIsNullabl);
			byte[] byteNull = new byte[(iColCount + 7) / 8];
			bitsNull.CopyTo(byteNull, 0);
			bw.Write(byteNull);

			///////////////////////////////////////////////////////////////
			// add data

			// count rows
			bw.Write(dt.Rows.Count);

			// ���������� ������
			foreach(DataRow dr in dt.Rows)
			{
				byte verFlags = 0;
				int iVerStart;
				int iVerEnd;
				// �����������, ����� ������ ����� ������.
				// ����� ���� ��� ��������: Original � Current
				DataRowState state = dr.RowState;
				switch(state)
				{
						// Original + Current � ��� �����!
					case DataRowState.Unchanged: 
						iVerStart = 0;
						iVerEnd = 0;
						verFlags = 0;
						break;
					case DataRowState.Deleted: // ������ Original
						iVerStart = 0;
						iVerEnd = 0;
						verFlags = 1;
						break;
					case DataRowState.Added: // ������ Current
						iVerStart = 1;
						iVerEnd = 1;
						verFlags = 2;
						break;
						// Original + Current � ��� �� �����!
					case DataRowState.Modified:
						iVerStart = 0;
						iVerEnd = 1;
						verFlags = 3;
						break;
					default:
						throw new ApplicationException(
							"������������ ��������� ������: " + state.ToString());
				}

				// ����� �������� ������. ��������, ��� ��� �� ���� ��
				// ������ ���� �� ������. ���� ����� ������ �������������� ���
				// ���� � ������� ���� DbNull (���� ��� � �� �������).
				bw.Write(verFlags);

				// ���������� ������ ������� ������. ����� �� ����� ���� ���.
				// � �������� ����� ���� �� ��� ������ DataRowState.Modified
				// ������ ������ ������ ������. �� ��� ���-������ �����. :)
				for(int iVetIndex = iVerStart; iVetIndex <= iVerEnd; iVetIndex++)
				{
					DataRowVersion drv = _aryVer[iVetIndex];

					// ������� � ��������� ������� ����. ���� ��� ������,
					// ������, ��������������� ������� �������� DBNull.
					bitsNull.SetAll(false);
					for(int i = 0; i < iColCount; i++)
					{
						if(dr[i, drv] == DBNull.Value)
							bitsNull.Set(i, true);
					}
					bitsNull.CopyTo(byteNull, 0);
					// ���������� ������� ���� � �����.
					bw.Write(byteNull);

					// ���������� ������� � ����� ������...
					for(int i = 0; i < iColCount; i++)
					{
						// ���� ������� �������� DBNull, ���������� �� �������� 
						// �������.
						object data = dr[i, drv];
						if(data == DBNull.Value) // ��������� ������!
							continue;

						// ���������� ������ ������.
						switch(colTypeCodes[i]) 
						{	// ������� ���� ������������� ���������������� �������...
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
								// ��� DateTime ���������� �������������� ������ �������.
								bw.Write(((DateTime)(data)).ToFileTime());
								break;
							case TypeCode.String: bw.Write((String)data); break;
							default:
								// �� ������ ������ ������� �������� ������������ ���
								// ���� ������.
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
		// ��������������

		public static DataTable DeserializeTable(BinaryReader br)
		{
			DataColumn dc;
			DataRow dr;

			// ��� DataTable ���� ���������� � �������� ��������� ������������.
			DataTable dt = new DataTable(br.ReadString());
			dt.BeginLoadData();

			int iColCount = br.ReadInt32();
			dt.MinimumCapacity = iColCount;

			// � ���� ������ ����� �������� TypeCode-� ��� ������� DataTable-�.
			TypeCode[] colTypeCodes = new TypeCode[iColCount];
			// � � ���� ������ ����� �������� ���� (Type) ������� DataTable-�
			// �������������� TypeCode-��.
			for(int c = 0; c < iColCount; c++)
			{
				string colName = br.ReadString();
				// ��������� TypeCode.
				TypeCode tc = (TypeCode)br.ReadInt32();
				// �������� TypeCode � ������ ��� ����������� �������������.
				colTypeCodes[c] = tc;
				// �������� (����� ���) ��� ��������������� TypeCode-�.
				Type type = _TypeMap[(int)tc];
				// ������� ������� � ���������� ������ � �����.
				dc = new DataColumn(colName, type);
				dt.Columns.Add(dc);
			}

				
			// ��������� ������ nullabl-�������.
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
				// ������ �������� ������. ��������, ��� ��� �� ���� ��
				// ������ ���� �� ������.
				byte verFlags = br.ReadByte();
				int iVerStart;
				int iVerEnd;
				DataRowState drs;
				switch(verFlags)
				{
						// Original + Current � ��� �����!
					case 0: // DataRowState.Unchanged
						iVerStart = 0;
						iVerEnd = 0;
						drs = DataRowState.Unchanged;
						break;
					case 1: // DataRowState.Deleted ������ Original
						iVerStart = 0;
						iVerEnd = 0;
						drs = DataRowState.Deleted;
						break;
					case 2: // DataRowState.Added ������ Current
						iVerStart = 1;
						iVerEnd = 1;
						drs = DataRowState.Added;
						break;
						// Original + Current � ��� �� �����!
					case 3: // DataRowState.Modified
						iVerStart = 0;
						iVerEnd = 1;
						drs = DataRowState.Modified;
						break;
					default:
						throw new ApplicationException(
							"������������ ��������� ������. ���� ��� ��������.");
				}
				
				// ��������� ������.
				dr = dt.NewRow();
				rows.Add(dr);
				dr.BeginEdit();

				// ��������� ������ ������� ������.
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
			// ��������� ��� DataSet � ������� ���...
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