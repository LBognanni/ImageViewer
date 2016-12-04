using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer
{
    public class InterruptibleBackgroundWorker : Component
    {
        private Control pUIContext;
        private Thread pThread;
        private DoWorkEventArgs pArgs;

        /// <summary>
        /// Async work task
        /// </summary>
        public event DoWorkEventHandler DoWork;

        /// <summary>
        /// Action to perform after completion
        /// </summary>
        public event RunWorkerCompletedEventHandler RunWorkerCompleted;

        private bool pThreadCompleted = false;
        private bool pThreadCanceled = false;

        public InterruptibleBackgroundWorker(Control uiContext)
        {
            pUIContext = uiContext;
        }

        public bool CancellationPending
        {
            get
            {
                return pThreadCanceled;
            }
        }

        /// <summary>
        /// Is the thread running?
        /// </summary>
        public bool IsBusy
        {
            get
            {
                return pThread != null && pThread.IsAlive;
            }
        }

        /// <summary>
        /// Run the async task
        /// </summary>
        public void RunWorkerAsync()
        {
            RunWorkerAsync(null);
        }

        /// <summary>
        /// Run the async task
        /// </summary>
        /// <param name="param"></param>
        public void RunWorkerAsync(object param)
        {
            if (this.DoWork != null)
            {
                pThreadCompleted = false;
                pThreadCanceled = false;
                pArgs = new DoWorkEventArgs(param);

                // Setup the thread
                pThread = new Thread(new ThreadStart(() =>
                   {
                       Exception resultEx = null;
                       try
                       {
                           // Do the async work
                           DoWork(this, pArgs);
                       }
                       catch(Exception ex)
                       {
                           resultEx = ex;
                       }

                       // Async work completed
                       pThreadCompleted = true;
                       // Call back the UI
                       pOnComplete(resultEx);
                   }));

                // Launch the async action
                pThread.Start();
            }
        }

        /// <summary>
        /// Cancel the thread if it is running
        /// </summary>
        public void CancelAsync()
        {
            if (pThread != null)
            {
                pThreadCanceled = true;
                if (!pThreadCompleted)
                {
                    pThread.Abort();
                    pThread = null;
                }
            }
        }

        /// <summary>
        /// Call the RunWorkerCompleted event on the UI thread
        /// </summary>
        private void pOnComplete(Exception ex)
        {
            if (this.RunWorkerCompleted != null)
            {
                if (pUIContext.Created)
                {
                    pUIContext.BeginInvoke(new MethodInvoker(() =>
                    {
                        this.RunWorkerCompleted(this, new RunWorkerCompletedEventArgs(pArgs.Result, ex, pThreadCanceled));
                    }));
                }
            }
        }

        internal void Wait()
        {
            while(pThread.IsAlive)
            {
                Application.DoEvents();
            }
        }
    }
}
