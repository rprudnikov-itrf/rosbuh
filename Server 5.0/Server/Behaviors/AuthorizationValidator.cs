#region Disclaimer
// THIS SOFTWARE COMES "AS IS", WITH NO WARRANTIES.  THIS
// MEANS NO EXPRESS, IMPLIED OR STATUTORY WARRANTY, INCLUDING
// WITHOUT LIMITATION, WARRANTIES OF MERCHANTABILITY OR FITNESS
// FOR A PARTICULAR PURPOSE OR ANY WARRANTY OF TITLE OR
// NON-INFRINGEMENT.
//
// MICROSOFT WILL NOT BE LIABLE FOR ANY DAMAGES RELATED TO
// THE SOFTWARE, INCLUDING DIRECT, INDIRECT, SPECIAL,
// CONSEQUENTIAL OR INCIDENTAL DAMAGES, TO THE MAXIMUM EXTENT
// THE LAW PERMITS, NO MATTER WHAT LEGAL THEORY IT IS
// BASED ON.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.ServiceModel;

namespace RosService.ServiceModel
{
    public class AuthorizationValidator : UserNamePasswordValidator
    {
        public override void Validate(string userName, string password)
        {
            if (null == userName || null == password)
            {
                throw new ArgumentNullException();
            }

            if (!(userName == "admin" && password == "12345"))
            {
                throw new SecurityTokenException("Unknown Username or Incorrect Password");
            }
        }
    }

    //public class CustomServiceAuthorizationManager : ServiceAuthorizationManager
    //{
    //    public override bool CheckAccess(OperationContext operationContext, ref System.ServiceModel.Channels.Message message)
    //    {
    //        return base.CheckAccess(operationContext, ref message);
    //    }
    //}
}
