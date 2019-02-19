using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RosService.HBaseCore
{
    // Класс передачи и приема данных от системы в клиент
    public class Row
    {
        public Row()
        {
        }

        public Row(Guid GuidCode, Dictionary<string, object> Значения, List<string> ИдентификаторыОбъекта)
        {
            this.GuidCode = GuidCode;
            this.Значения = Значения ?? new Dictionary<string, object>();
            this.ИдентификаторыОбъекта = ИдентификаторыОбъекта ?? new List<string>();
        }

        public Guid GuidCode { get; set; }
        public Dictionary<string, object> Значения { get; set; }

        // Сюда пишим все доп индентификаторы объекта: id_node, ИдентификаторОбъекта 
        public List<string> ИдентификаторыОбъекта { get; set; }
    }
}
