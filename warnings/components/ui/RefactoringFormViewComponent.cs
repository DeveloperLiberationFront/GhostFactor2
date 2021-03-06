﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using BlackHen.Threading;
using NLog;
using Roslyn.Services;
using Roslyn.Services.Editor;
using warnings.refactoring;
using warnings.ui;
using warnings.util;

namespace warnings.components.ui
{
    public interface IUIComponent
    {
    }

    /* delegate for update a control component. */
    public delegate void UIUpdate();

    /// <summary>
    ///  This the view part in the MVC pattern. It registers to the event of code issue changes. 
    ///  When code issues change, this component will ask the latest issues and update the form.
    /// </summary>
    internal class RefactoringFormViewComponent : IUIComponent
    {
        /* Singleton this component. */
        private static RefactoringFormViewComponent instance = 
            new RefactoringFormViewComponent();

        public static IUIComponent GetInstance()
        {
            return instance;
        }

        
        /* A work queue for short running task, such as updating items to the form. */
        private WorkQueue shortTaskQueue;
       
        /* The form instance where new warnings should be added to. */
        private RefactoringWariningsForm form;

        private RefactoringFormViewComponent()
        {
            form = new RefactoringWariningsForm();
            shortTaskQueue = GhostFactorComponents.configurationComponent.GetGlobalWorkQueue();
         
            GhostFactorComponents.historyComponent.OnWorkDocumentChanged += OnWorkDocumentChanged;
            GhostFactorComponents.configurationComponent.supportedRefactoringTypesChangedEvent += 
                RefactoringTypesChangedEvent;

            // Create an work item for showing dialog and add this work item
            // to the work longRunningQueue.
            new Thread(ShowingForm).Start();
        }

        private void ShowingForm()
        {
            form.ShowDialog();
        }

        private void RefactoringTypesChangedEvent(IEnumerable<RefactoringType> currentTypes)
        {
            shortTaskQueue.Add(new UpdatedSupportedRefactoringTypesWorkItem(form, currentTypes));
        }

        private void OnWorkDocumentChanged(IDocument document)
        {
            shortTaskQueue.Add(new UpdateWorkDocumentWorkItem(form, document));
        }

        /* Work item for adding refactoring errors in the form. */
        private class AddWarningsWorkItem : WorkItem
        {
            private readonly RefactoringWariningsForm form;
            private readonly Logger logger;
            private readonly IEnumerable<IRefactoringWarningMessage> messages;

            internal AddWarningsWorkItem(RefactoringWariningsForm form, 
                IEnumerable<IRefactoringWarningMessage> messages)
            {
                this.form = form;
                this.messages = messages;
                this.logger = NLoggerUtil.GetNLogger(typeof (AddWarningsWorkItem));
            }

            public override void Perform()
            {
                // Add messages to the form. 
                form.Invoke(new UIUpdate(AddRefactoringWarnings));
            }

            private void AddRefactoringWarnings()
            {
                logger.Info("Adding messages to the form.");
                form.AddRefactoringWarnings(messages);
            }
        }

        /* This work item is for removing warnings in the refactoring warning list. */
        private class RemoveWarningsWorkItem : WorkItem
        {
            private readonly Predicate<IRefactoringWarningMessage> removeCondition;
            private readonly RefactoringWariningsForm form;

            internal RemoveWarningsWorkItem(RefactoringWariningsForm form, 
                Predicate<IRefactoringWarningMessage> removeCondition)
            {
                this.form = form;
                this.removeCondition = removeCondition;
            }

            public override void Perform()
            {
                // Invoke the delegate method to remove warnings.
                form.Invoke(new UIUpdate(RemoveWarnings));
            }

            private void RemoveWarnings()
            {
                form.RemoveRefactoringWarnings(removeCondition);
            }
        }

        /* Reset the refactoring count showing in the form. */
        private class ResetRefactoringCountWorkItem : WorkItem
        {
            private readonly RefactoringWariningsForm form;
            private readonly int newCount;

            internal ResetRefactoringCountWorkItem(RefactoringWariningsForm form, int newCount)
            {
                this.form = form;
                this.newCount = newCount;
            }

            public override void Perform()
            {
                form.Invoke(new UIUpdate(ResetRefactoringCount));
            }

            private void ResetRefactoringCount()
            {
                form.SetProblematicRefactoringsCount(newCount);
            }
        }
        /// <summary>
        /// Work iten for updating the active document indicator.
        /// </summary>
        private class UpdateWorkDocumentWorkItem : WorkItem
        {
            private readonly IDocument document;
            private readonly RefactoringWariningsForm form;

            public UpdateWorkDocumentWorkItem(RefactoringWariningsForm form, IDocument document)
            {
                this.form = form;
                this.document = document;
            }

            public override void Perform()
            {
                form.Invoke(new UIUpdate(ResetWorkOnDocument));
            }

            private void ResetWorkOnDocument()
            {
                form.SetActiveDocumentText(document.Name);
            }
        }

        /// <summary>
        /// Work item that updates the ui's showing of current supported refactoring type.
        /// </summary>
        private class UpdatedSupportedRefactoringTypesWorkItem : WorkItem
        {
            private readonly RefactoringWariningsForm form;
            private readonly IEnumerable<RefactoringType> currentTypes;

            public UpdatedSupportedRefactoringTypesWorkItem(RefactoringWariningsForm form, 
                IEnumerable<RefactoringType> currentTypes)
            {
                this.form = form;
                this.currentTypes = currentTypes;
            }

            public override void Perform()
            {
                form.Invoke(new UIUpdate(UpdateSupportedTypes));
            }

            private void UpdateSupportedTypes()
            {
                form.SetSupportedRefactoringTypes(currentTypes);
            }
        }
    }
}
