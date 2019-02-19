using System;
using RosService.Data;
using System.Data;
using System.Text.RegularExpressions;
using System.Web.UI.WebControls;
using System.Web.UI;
using System.Collections.Generic;

namespace RosService.Web
{
    public static class Helper
    {
        /// <summary>
        /// Загузить значения в html каротчку
        /// </summary>
        public static void ПолучитьЗначение(decimal id_node, string[] attributes, Хранилище хранилище, System.Web.UI.Control content)
        {
            using (RosService.Client client = new RosService.Client())
            {
                foreach (var item in client.Архив.ПолучитьЗначение(id_node, attributes, хранилище))
                {
                    var control = content.FindControl(Regex.Replace(item.Key.ToString(), "[./]", "_"));
                    if (control == null) continue;


                    if (control is System.Web.UI.WebControls.ListControl)
                    {
                        var items = control as System.Web.UI.WebControls.ListControl;
                        if (items.DataSource != null && items.Items.Count == 0)
                            items.DataBind();

                        var str = (item.Value is string) ? (string)item.Value : string.Format("{0:f0}", item.Value);
                        var ListItem = items.Items.FindByValue(str);
                        if (ListItem != null) items.SelectedValue = str;
                    }
                    else if (control is System.Web.UI.ITextControl)
                    {
                        try
                        {
                            if (item.Value is DateTime)
                            {
                                (control as System.Web.UI.ITextControl).Text = Convert.ToDateTime(item.Value).ToShortDateString();
                            }
                            else if (item.Value is double || item.Value is decimal)
                            {
                                (control as System.Web.UI.ITextControl).Text = Convert.ToDouble(item.Value).ToString("N2");
                            }
                            else
                            {
                                (control as System.Web.UI.ITextControl).Text = item.Value.ToString();
                            }
                        }
                        catch
                        {
                            (control as System.Web.UI.ITextControl).Text = null;
                        }
                    }
                    else if (control is System.Web.UI.WebControls.Repeater)
                    {
                        (control as System.Web.UI.WebControls.Repeater).DataSource = item.Value;
                    }
                    else if (control is System.Web.UI.WebControls.DataList)
                    {
                        (control as System.Web.UI.WebControls.DataList).DataSource = item.Value;
                    }
                }
            }
        }
        public static void СохранитьЗначение(decimal id_node, string[] attributes, Хранилище хранилище, System.Web.UI.Control content)
        {
            using (RosService.Client client = new RosService.Client())
            {
                var values = new Dictionary<string, object>();
                foreach (var item in client.Архив.ПолучитьЗначение(id_node, attributes))
                {
                    var control = content.FindControl(Regex.Replace(item.Key.ToString(), "[./]", "_"));
                    if (control == null) continue;

                    if (control is ListControl)
                    {
                        if (Convert.ToString(item.Value) != (control as ListControl).SelectedValue)
                            values.Add(item.Key, (control as ListControl).SelectedValue);
                    }
                    else if (control is ITextControl)
                    {
                        if (control is TextBox && ((TextBox)control).ReadOnly) continue;
                        if (Convert.ToString(item.Value) != (control as ITextControl).Text)
                            values.Add(item.Key, (control as ITextControl).Text);
                    }
                    else if (control is System.Web.UI.WebControls.Repeater)
                    {
                        if((control as System.Web.UI.WebControls.Repeater).DataSource is DataTable ||
                            (control as System.Web.UI.WebControls.Repeater).DataSource is DataView)
                            (control as System.Web.UI.WebControls.Repeater).DataSource = item.Value;
                    }
                    else if (control is System.Web.UI.WebControls.BaseDataList)
                    {
                        if ((control as System.Web.UI.WebControls.BaseDataList).DataSource is DataTable ||
                            (control as System.Web.UI.WebControls.BaseDataList).DataSource is DataView)
                            (control as System.Web.UI.WebControls.BaseDataList).DataSource = item.Value;
                    }
                }
                client.Архив.СохранитьЗначение(id_node, values, хранилище);
            }
        }
    }
}
