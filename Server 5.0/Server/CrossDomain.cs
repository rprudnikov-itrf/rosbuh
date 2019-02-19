using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RosService.Intreface;
using System.ServiceModel;
using System.IO;
using System.ServiceModel.Web;

namespace RosService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
    ConcurrencyMode = ConcurrencyMode.Multiple,
    UseSynchronizationContext = false)]
    public class CrossDomain : ICrossDomain
    {
        public string Echo(string text) { return text; }
        Stream StringToStream(string result)
        {
            WebOperationContext.Current.OutgoingResponse.ContentType = "application/xml";
            return new MemoryStream(Encoding.UTF8.GetBytes(result));
        }
        public Stream GetSilverlightPolicy()
        {
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<access-policy>
	<cross-domain-access>
        <!--Enables Silverlight 4 all methods functionality-->
        <policy>
          <allow-from http-request-headers=""SOAPAction"">
            <domain uri=""*""/>
          </allow-from>
          <grant-to>
            <resource path=""/"" include-subpaths=""true""/>
          </grant-to>
        </policy>

		<!--Enables Silverlight 3 all methods functionality-->
		<policy>
			<allow-from http-methods=""*"">
				<domain uri=""*""/>
			</allow-from>
			<grant-to>
				<resource path=""/"" include-subpaths=""true""/>
			</grant-to>
		</policy>

		<!--Enables Silverlight 2 clients to continue to work normally -->
		<policy>
			<allow-from>
				<domain uri=""*""/>
			</allow-from>
			<grant-to>
				<resource path=""/"" include-subpaths=""true""/>
			</grant-to>
		</policy>
	</cross-domain-access>
</access-policy>";
            return StringToStream(result);
        }
        public Stream GetFlashPolicy()
        {
            string result = @"<?xml version=""1.0""?>
<!DOCTYPE cross-domain-policy SYSTEM ""http://www.macromedia.com/xml/dtds/cross-domain-policy.dtd"">
<cross-domain-policy>
    <allow-access-from domain=""*"" />
</cross-domain-policy>";
            return StringToStream(result);
        }
    }
}
