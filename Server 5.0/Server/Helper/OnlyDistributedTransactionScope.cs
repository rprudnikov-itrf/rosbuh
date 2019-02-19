using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;

namespace RosService.Helper
{
    internal class OnlyDistributedTransactionScope : IDisposable
    {
        private TransactionScope m_scope;
        internal OnlyDistributedTransactionScope()
        {
            if (Transaction.Current != null
                    && Transaction.Current.TransactionInformation.DistributedIdentifier == Guid.Empty)
            {
                m_scope = new TransactionScope(TransactionScopeOption.Suppress);
            }
        }
        ~OnlyDistributedTransactionScope()
        {
            Dispose(false);
        }
        protected void Dispose(bool disposing)
        {
            if (disposing && m_scope != null)
            {
                m_scope.Dispose();
                m_scope = null;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
