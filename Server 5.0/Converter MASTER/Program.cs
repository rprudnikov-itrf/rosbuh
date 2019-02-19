using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace Converter_MASTER
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Threading.Tasks.Parallel.Invoke(
                () =>
                {
                    Node(1);
                },
                () =>
                {
                    Node(2);
                },
                () =>
                {
                    Node(3);
                },
                () =>
                {
                    Node(4);
                },
                () =>
                {
                    Node(5);
                },
                () =>
                {
                    Node(6);
                },
                () =>
                {
                    Node(7);
                },
                () =>
                {
                    Node(8);
                },
                () =>
                {
                    Node(9);
                });

            Console.WriteLine("END!!!");
            Console.ReadKey();
        }

        static void Node(int node)
        {
            using (var __conn = new System.Data.SqlClient.SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["sConn"].ConnectionString))
            using (var __command = __conn.CreateCommand())
            {
                try
                {
                    var sb = new StringBuilder(200);
                    sb.AppendLine("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
                    sb.AppendLine("SET NOCOUNT ON;");

                    sb.Append(@"
declare @svalue int, @evalue int

select 
	@svalue = [id_value],
	@evalue = @node * 15000000
from
	[_value]
where
	[id] = @node
	
	select @svalue, @evalue

declare 
	@id_node int,
	@type nvarchar(128),
	@double_value numeric(28, 13),
	@datetime_value datetime,
	@string_value nvarchar(max),
	@string_value_index nvarchar(512),
	@byte_value varbinary(max),
	@hide bit,
	@attr_type int,
	@id_value int

declare ecase cursor local fast_forward
    for        
		SELECT 
		       [id_value]
			  ,[id_node]
			  ,[type]
			  ,[double_value]
			  ,[datetime_value]
			  ,[string_value]
			  ,[string_value_index]
			  ,[byte_value]
			  ,[hide]
		  FROM [мастер4].[dbo].[tblValue]
		  where id_value > @svalue and id_value < @evalue	  
		  
open ecase
fetch next from ecase into @id_value, @id_node, @type, @double_value, @datetime_value, @string_value, @string_value_index, @byte_value, @hide
while @@fetch_status = 0
begin

	select @attr_type = MemberType from [dbo].[assembly_tblAttribute] with(nolock) where Name = @type 
	
	/*if @attr_type = 2
		insert into tblValueString values(@id_node, @type, @string_value, @string_value_index, @hide)
	
	else if @attr_type = 3
		insert into tblValueDouble values(@id_node, @type, @double_value, @hide)
		
	else if @attr_type = 6
		insert into tblValueBool values(@id_node, @type, cast(isnull(@double_value, 0) as bit), @hide)
		
	else if @attr_type = 7
		insert into tblValueHref values(@id_node, @type, @double_value, @string_value_index, @hide)
		
	else if @attr_type = 5
		insert into tblValueDate values(@id_node, @type, @datetime_value, @hide)
		
	else if @attr_type = 9
		insert into tblValueByte values(@id_node, @type, @byte_value, @hide)

    else*/ if @attr_type = 4
		insert into tblValueDouble values(@id_node, @type, @double_value, @hide)
		
	update [_value] set id_value = @id_value where id = @node

	fetch next from ecase into @id_value, @id_node, @type, @double_value, @datetime_value, @string_value, @string_value_index, @byte_value, @hide
end
close ecase
deallocate ecase
");

                    __command.Parameters.AddWithValue("@node", node).SqlDbType = SqlDbType.Int;
                    __command.CommandTimeout = int.MaxValue;
                    __command.CommandText = sb.ToString();

                    Console.WriteLine("Start Node [{0}]!!!!!!!", node);
                    __conn.Open();
                    __command.ExecuteNonQuery();

                    Console.WriteLine("END Node [{0}]!!!!!!!", node);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Node [{0}]\n\n{1}", node, ex.ToString());
                }
                finally
                {
                    if (__conn.State != System.Data.ConnectionState.Closed)
                        __conn.Close();
                }
            }
        }
    }
}
