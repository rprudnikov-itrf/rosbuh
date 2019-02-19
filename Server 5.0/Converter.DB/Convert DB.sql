

declare 
	@id_node int,
	@type nvarchar(128),
	@double_value numeric(28, 13),
	@datetime_value datetime,
	@string_value nvarchar(max),
	@string_value_index nvarchar(512),
	@byte_value varbinary(max),
	@hide bit,
	@attr_type int

declare ecase cursor local fast_forward
    for        
		SELECT 
			   [id_node]
			  ,[type]
			  ,[double_value]
			  ,[datetime_value]
			  ,[string_value]
			  ,[string_value_index]
			  ,[byte_value]
			  ,[hide]
		  FROM [мастер].[dbo].[tblValue]
open ecase
fetch next from ecase into @id_node, @type, @double_value, @datetime_value, @string_value, @string_value_index, @byte_value, @hide
while @@fetch_status = 0
begin

	select @attr_type = MemberType from [мастер].[dbo].[assembly_tblAttribute] where Name = @type
	
	if @attr_type = 2
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

	fetch next from ecase into @id_node, @type, @double_value, @datetime_value, @string_value, @string_value_index, @byte_value, @hide
end
close ecase
deallocate ecase