using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public class UsingLock
    {
        private ReaderWriterLockSlim _rwl = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private struct Lock : IDisposable
        {
            private bool _isWrite;
            private ReaderWriterLockSlim internalRwl;
            private bool _upgradeable;

            public Lock(ReaderWriterLockSlim rwl, bool isWrite, bool upgradeable = false)
            {
                _isWrite = isWrite;
                internalRwl = rwl;
                _upgradeable = upgradeable;
            }

            public void Dispose()
            {
                if (_isWrite)
                {
                    if (internalRwl.IsWriteLockHeld)
                        internalRwl.ExitWriteLock();
                }
                else
                {
                    if (!_upgradeable)
                    {
                        if (internalRwl.IsReadLockHeld)
                            internalRwl.ExitReadLock();
                    }
                    else
                    {
                        internalRwl.ExitUpgradeableReadLock();
                    }
                }
            }
        }

        public IDisposable Write()
        {
            _rwl.EnterWriteLock();
            return new Lock(_rwl, true);
        }

        public IDisposable Read()
        {
            _rwl.EnterReadLock();
            return new Lock(_rwl, false);
        }

        public IDisposable UpgradeableRead()
        {
            _rwl.EnterUpgradeableReadLock();
            return new Lock(_rwl, false, true);
        }

    }
}
