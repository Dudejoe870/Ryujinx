using ARMeilleure.Memory;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Ipc;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.Threading;

namespace Ryujinx.HLE.HOS.Kernel.SupervisorCall
{
    partial class SvcHandler
    {
        public void ExitProcess64()
        {
            ExitProcess();
        }

        private void ExitProcess()
        {
            _system.Scheduler.GetCurrentProcess().Terminate();
        }

        public KernelResult SignalEvent64(int handle)
        {
            return SignalEvent(handle);
        }

        private KernelResult SignalEvent(int handle)
        {
            KWritableEvent writableEvent = _process.HandleTable.GetObject<KWritableEvent>(handle);

            KernelResult result;

            if (writableEvent != null)
            {
                writableEvent.Signal();

                result = KernelResult.Success;
            }
            else
            {
                result = KernelResult.InvalidHandle;
            }

            return result;
        }

        public KernelResult ClearEvent64(int handle)
        {
            return ClearEvent(handle);
        }

        private KernelResult ClearEvent(int handle)
        {
            KernelResult result;

            KWritableEvent writableEvent = _process.HandleTable.GetObject<KWritableEvent>(handle);

            if (writableEvent == null)
            {
                KReadableEvent readableEvent = _process.HandleTable.GetObject<KReadableEvent>(handle);

                result = readableEvent?.Clear() ?? KernelResult.InvalidHandle;
            }
            else
            {
                result = writableEvent.Clear();
            }

            return result;
        }

        public KernelResult CloseHandle64(int handle)
        {
            return CloseHandle(handle);
        }

        private KernelResult CloseHandle(int handle)
        {
            KAutoObject obj = _process.HandleTable.GetObject<KAutoObject>(handle);

            _process.HandleTable.CloseHandle(handle);

            if (obj == null)
            {
                return KernelResult.InvalidHandle;
            }

            if (obj is KSession session)
            {
                session.Dispose();
            }
            else if (obj is KTransferMemory transferMemory)
            {
                _process.MemoryManager.ResetTransferMemory(
                    transferMemory.Address,
                    transferMemory.Size);
            }

            return KernelResult.Success;
        }

        public KernelResult ResetSignal64(int handle)
        {
            return ResetSignal(handle);
        }

        private KernelResult ResetSignal(int handle)
        {
            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            KReadableEvent readableEvent = currentProcess.HandleTable.GetObject<KReadableEvent>(handle);

            KernelResult result;

            if (readableEvent != null)
            {
                result = readableEvent.ClearIfSignaled();
            }
            else
            {
                KProcess process = currentProcess.HandleTable.GetKProcess(handle);

                if (process != null)
                {
                    result = process.ClearIfNotExited();
                }
                else
                {
                    result = KernelResult.InvalidHandle;
                }
            }

            return result;
        }

        public ulong GetSystemTick64()
        {
            return _system.Scheduler.GetCurrentThread().Context.CntpctEl0;
        }

        public KernelResult GetProcessId64(int handle, out long pid)
        {
            return GetProcessId(handle, out pid);
        }

        private KernelResult GetProcessId(int handle, out long pid)
        {
            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            KProcess process = currentProcess.HandleTable.GetKProcess(handle);

            if (process == null)
            {
                KThread thread = currentProcess.HandleTable.GetKThread(handle);

                if (thread != null)
                {
                    process = thread.Owner;
                }

                // TODO: KDebugEvent.
            }

            pid = process?.Pid ?? 0;

            return process != null
                ? KernelResult.Success
                : KernelResult.InvalidHandle;
        }

        public void Break64(ulong reason, ulong x1, ulong info)
        {
            Break(reason);
        }

        private void Break(ulong reason)
        {
            KThread currentThread = _system.Scheduler.GetCurrentThread();

            if ((reason & (1UL << 31)) == 0)
            {
                currentThread.PrintGuestStackTrace();

                throw new GuestBrokeExecutionException();
            }
            else
            {
                Logger.PrintInfo(LogClass.KernelSvc, "Debugger triggered.");

                currentThread.PrintGuestStackTrace();
            }
        }

        public void OutputDebugString64(ulong strPtr, ulong size)
        {
            OutputDebugString(strPtr, size);
        }

        private void OutputDebugString(ulong strPtr, ulong size)
        {
            string str = MemoryHelper.ReadAsciiString(_process.CpuMemory, (long)strPtr, (long)size);

            Logger.PrintWarning(LogClass.KernelSvc, str);
        }

        public KernelResult GetInfo64(uint id, int handle, long subId, out long value)
        {
            return GetInfo(id, handle, subId, out value);
        }

        private KernelResult GetInfo(uint id, int handle, long subId, out long value)
        {
            value = 0;

            switch (id)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 12:
                case 13:
                case 14:
                case 15:
                case 16:
                case 17:
                case 18:
                case 20:
                case 21:
                case 22:
                {
                    if (subId != 0)
                    {
                        return KernelResult.InvalidCombination;
                    }

                    KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

                    KProcess process = currentProcess.HandleTable.GetKProcess(handle);

                    if (process == null)
                    {
                        return KernelResult.InvalidHandle;
                    }

                    switch (id)
                    {
                        case 0: value = process.Capabilities.AllowedCpuCoresMask;    break;
                        case 1: value = process.Capabilities.AllowedThreadPriosMask; break;

                        case 2: value = (long)process.MemoryManager.AliasRegionStart; break;
                        case 3: value = (long)(process.MemoryManager.AliasRegionEnd -
                                               process.MemoryManager.AliasRegionStart); break;

                        case 4: value = (long)process.MemoryManager.HeapRegionStart; break;
                        case 5: value = (long)(process.MemoryManager.HeapRegionEnd -
                                               process.MemoryManager.HeapRegionStart); break;

                        case 6: value = (long)process.GetMemoryCapacity(); break;

                        case 7: value = (long)process.GetMemoryUsage(); break;

                        case 12: value = (long)process.MemoryManager.GetAddrSpaceBaseAddr(); break;

                        case 13: value = (long)process.MemoryManager.GetAddrSpaceSize(); break;

                        case 14: value = (long)process.MemoryManager.StackRegionStart; break;
                        case 15: value = (long)(process.MemoryManager.StackRegionEnd -
                                                process.MemoryManager.StackRegionStart); break;

                        case 16: value = (long)process.PersonalMmHeapPagesCount * KMemoryManager.PageSize; break;

                        case 17:
                            if (process.PersonalMmHeapPagesCount != 0)
                            {
                                value = process.MemoryManager.GetMmUsedPages() * KMemoryManager.PageSize;
                            }

                            break;

                        case 18: value = (long)process.TitleId; break;

                        case 20: value = (long)process.UserExceptionContextAddress; break;

                        case 21: value = (long)process.GetMemoryCapacityWithoutPersonalMmHeap(); break;

                        case 22: value = (long)process.GetMemoryUsageWithoutPersonalMmHeap(); break;
                    }

                    break;
                }

                case 8:
                {
                    if (handle != 0)
                    {
                        return KernelResult.InvalidHandle;
                    }

                    if (subId != 0)
                    {
                        return KernelResult.InvalidCombination;
                    }

                    value = _system.Scheduler.GetCurrentProcess().Debug ? 1 : 0;

                    break;
                }

                case 9:
                {
                    if (handle != 0)
                    {
                        return KernelResult.InvalidHandle;
                    }

                    if (subId != 0)
                    {
                        return KernelResult.InvalidCombination;
                    }

                    KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

                    if (currentProcess.ResourceLimit != null)
                    {
                        KHandleTable   handleTable   = currentProcess.HandleTable;
                        KResourceLimit resourceLimit = currentProcess.ResourceLimit;

                        KernelResult result = handleTable.GenerateHandle(resourceLimit, out int resLimHandle);

                        if (result != KernelResult.Success)
                        {
                            return result;
                        }

                        value = (uint)resLimHandle;
                    }

                    break;
                }

                case 10:
                {
                    if (handle != 0)
                    {
                        return KernelResult.InvalidHandle;
                    }

                    int currentCore = _system.Scheduler.GetCurrentThread().CurrentCore;

                    if (subId != -1 && subId != currentCore)
                    {
                        return KernelResult.InvalidCombination;
                    }

                    value = _system.Scheduler.CoreContexts[currentCore].TotalIdleTimeTicks;

                    break;
                }

                case 11:
                {
                    if (handle != 0)
                    {
                        return KernelResult.InvalidHandle;
                    }

                    if ((ulong)subId > 3)
                    {
                        return KernelResult.InvalidCombination;
                    }

                    KProcess currentProcess = _system.Scheduler.GetCurrentProcess();


                    value = currentProcess.RandomEntropy[subId];

                    break;
                }

                case 0xf0000002u:
                {
                    if (subId < -1 || subId > 3)
                    {
                        return KernelResult.InvalidCombination;
                    }

                    KThread thread = _system.Scheduler.GetCurrentProcess().HandleTable.GetKThread(handle);

                    if (thread == null)
                    {
                        return KernelResult.InvalidHandle;
                    }

                    KThread currentThread = _system.Scheduler.GetCurrentThread();

                    int currentCore = currentThread.CurrentCore;

                    if (subId != -1 && subId != currentCore)
                    {
                        return KernelResult.Success;
                    }

                    KCoreContext coreContext = _system.Scheduler.CoreContexts[currentCore];

                    long timeDelta = PerformanceCounter.ElapsedMilliseconds - coreContext.LastContextSwitchTime;

                    if (subId != -1)
                    {
                        value = KTimeManager.ConvertMillisecondsToTicks(timeDelta);
                    }
                    else
                    {
                        long totalTimeRunning = thread.TotalTimeRunning;

                        if (thread == currentThread)
                        {
                            totalTimeRunning += timeDelta;
                        }

                        value = KTimeManager.ConvertMillisecondsToTicks(totalTimeRunning);
                    }

                    break;
                }

                default: return KernelResult.InvalidEnumValue;
            }

            return KernelResult.Success;
        }

        public KernelResult CreateEvent64(out int wEventHandle, out int rEventHandle)
        {
            return CreateEvent(out wEventHandle, out rEventHandle);
        }

        private KernelResult CreateEvent(out int wEventHandle, out int rEventHandle)
        {
            KEvent Event = new KEvent(_system);

            KernelResult result = _process.HandleTable.GenerateHandle(Event.WritableEvent, out wEventHandle);

            if (result == KernelResult.Success)
            {
                result = _process.HandleTable.GenerateHandle(Event.ReadableEvent, out rEventHandle);

                if (result != KernelResult.Success)
                {
                    _process.HandleTable.CloseHandle(wEventHandle);
                }
            }
            else
            {
                rEventHandle = 0;
            }

            return result;
        }

        public KernelResult GetProcessList64(ulong address, int maxCount, out int count)
        {
            return GetProcessList(address, maxCount, out count);
        }

        private KernelResult GetProcessList(ulong address, int maxCount, out int count)
        {
            count = 0;

            if ((maxCount >> 28) != 0)
            {
                return KernelResult.MaximumExceeded;
            }

            if (maxCount != 0)
            {
                KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

                ulong copySize = (ulong)maxCount * 8;

                if (address + copySize <= address)
                {
                    return KernelResult.InvalidMemState;
                }

                if (currentProcess.MemoryManager.OutsideAddrSpace(address, copySize))
                {
                    return KernelResult.InvalidMemState;
                }
            }

            int copyCount = 0;

            lock (_system.Processes)
            {
                foreach (KProcess process in _system.Processes.Values)
                {
                    if (copyCount < maxCount)
                    {
                        if (!KernelTransfer.KernelToUserInt64(_system, address + (ulong)copyCount * 8, process.Pid))
                        {
                            return KernelResult.UserCopyFailed;
                        }
                    }

                    copyCount++;
                }
            }

            count = copyCount;

            return KernelResult.Success;
        }

        public KernelResult GetSystemInfo64(uint id, int handle, long subId, out long value)
        {
            return GetSystemInfo(id, handle, subId, out value);
        }

        private KernelResult GetSystemInfo(uint id, int handle, long subId, out long value)
        {
            value = 0;

            if (id > 2)
            {
                return KernelResult.InvalidEnumValue;
            }

            if (handle != 0)
            {
                return KernelResult.InvalidHandle;
            }

            if (id < 2)
            {
                if ((ulong)subId > 3)
                {
                    return KernelResult.InvalidCombination;
                }

                KMemoryRegionManager region = _system.MemoryRegions[subId];

                switch (id)
                {
                    // Memory region capacity.
                    case 0: value = (long)region.Size; break;

                    // Memory region free space.
                    case 1:
                    {
                        ulong freePagesCount = region.GetFreePages();

                        value = (long)(freePagesCount * KMemoryManager.PageSize);

                        break;
                    }
                }
            }
            else /* if (Id == 2) */
            {
                if ((ulong)subId > 1)
                {
                    return KernelResult.InvalidCombination;
                }

                switch (subId)
                {
                    case 0: value = _system.PrivilegedProcessLowestId;  break;
                    case 1: value = _system.PrivilegedProcessHighestId; break;
                }
            }

            return KernelResult.Success;
        }
    }
}
