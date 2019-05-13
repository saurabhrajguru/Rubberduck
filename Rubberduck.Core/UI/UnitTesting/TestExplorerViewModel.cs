﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using NLog;
using Rubberduck.Common;
using Rubberduck.Interaction.Navigation;
using Rubberduck.Settings;
using Rubberduck.SettingsProvider;
using Rubberduck.UI.Command;
using Rubberduck.UI.Settings;
using Rubberduck.UI.UnitTesting.Commands;
using Rubberduck.UI.UnitTesting.ViewModels;
using Rubberduck.UnitTesting;
using Rubberduck.VBEditor.ComManagement;
using Rubberduck.VBEditor.Utility;
using DataFormats = System.Windows.DataFormats;
using Rubberduck.Resources.UnitTesting;

namespace Rubberduck.UI.UnitTesting
{
    public enum TestExplorerGrouping
    {
        None,
        Outcome,
        Category,
        Location
    }

    internal sealed class TestExplorerViewModel : ViewModelBase, INavigateSelection, IDisposable
    {
        private readonly IClipboardWriter _clipboard;
        private readonly ISettingsFormFactory _settingsFormFactory;

        public TestExplorerViewModel(ISelectionService selectionService,
            TestExplorerModel model,
            IClipboardWriter clipboard,
            // ReSharper disable once UnusedParameter.Local - left in place because it will likely be needed for app wide font settings, etc.
            IConfigurationService<Configuration> configService,
            ISettingsFormFactory settingsFormFactory)
        {
            _clipboard = clipboard;
            _settingsFormFactory = settingsFormFactory;

            NavigateCommand = new NavigateCommand(selectionService);  
            RunSingleTestCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), ExecuteSingleTestCommand, CanExecuteSingleTest);
            RunSelectedTestsCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), ExecuteSelectedTestsCommand, CanExecuteSelectedTestsCommand);
            RunSelectedGroupCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), ExecuteRunSelectedGroupCommand, CanExecuteSelectedGroupCommand);
            CancelTestRunCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), ExecuteCancelTestRunCommand, CanExecuteCancelTestRunCommand);
            ResetResultsCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), ExecuteResetResultsCommand, CanExecuteResetResultsCommand);
            CopyResultsCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), ExecuteCopyResultsCommand);
            OpenTestSettingsCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), OpenSettings);
            CollapseAllCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), ExecuteCollapseAll);
            ExpandAllCommand = new DelegateCommand(LogManager.GetCurrentClassLogger(), ExecuteExpandAll);

            Model = model;
            Model.TestCompleted += HandleTestCompletion;

            if (CollectionViewSource.GetDefaultView(Model.Tests) is ListCollectionView tests)
            {
                tests.SortDescriptions.Add(new SortDescription("QualifiedName.QualifiedModuleName.Name", ListSortDirection.Ascending));
                tests.SortDescriptions.Add(new SortDescription("QualifiedName.MemberName", ListSortDirection.Ascending));
                Tests = tests;
            }

            OnPropertyChanged(nameof(Tests));
            TestGrouping = TestExplorerGrouping.Outcome;

            OutcomeFilters = new System.Collections.ObjectModel.ObservableCollection<string>(
                new[] { _allResultsFilter }
                    .Concat(Enum.GetNames(typeof(TestOutcome)).Select(s => s.ToString()))
                    .OrderBy(s => s));
        }

        public TestExplorerModel Model { get; }

        public ICollectionView Tests { get; }

        public INavigateSource SelectedItem => MouseOverTest;

        private TestMethodViewModel _mouseOverTest;
        public TestMethodViewModel MouseOverTest
        {
            get => _mouseOverTest;
            set
            {
                if (ReferenceEquals(_mouseOverTest, value))
                {
                    return;
                }
                _mouseOverTest = value;
                OnPropertyChanged();
            }
        }

        private CollectionViewGroup _mouseOverGroup;
        public CollectionViewGroup MouseOverGroup
        {
            get => _mouseOverGroup;
            set
            {
                if (ReferenceEquals(_mouseOverGroup, value))
                {
                    return;
                }
                _mouseOverGroup = value;
                OnPropertyChanged();
            }
        }

        private static readonly Dictionary<TestExplorerGrouping, PropertyGroupDescription> GroupDescriptions = new Dictionary<TestExplorerGrouping, PropertyGroupDescription>
        {
            { TestExplorerGrouping.Outcome, new PropertyGroupDescription("Result.Outcome", new TestResultToOutcomeTextConverter()) },
            { TestExplorerGrouping.Location, new PropertyGroupDescription("QualifiedName.QualifiedModuleName.Name") },
            { TestExplorerGrouping.Category, new PropertyGroupDescription("Method.Category.Name") }
        };

        private TestExplorerGrouping _grouping = TestExplorerGrouping.None;

        public TestExplorerGrouping TestGrouping
        {
            get => _grouping;
            set
            {
                if (value == _grouping)
                {
                    return;
                }

                _grouping = value;
                Tests.GroupDescriptions.Clear();
                Tests.GroupDescriptions.Add(GroupDescriptions[_grouping]);
                Tests.Refresh();
                OnPropertyChanged();
            }
        }

        private bool _expanded;
        public bool ExpandedState
        {
            get => _expanded;
            set
            {
                _expanded = value;
                OnPropertyChanged();
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<string> OutcomeFilters { get; }

        private string _testNameFilter = string.Empty;
        public string TestNameFilter
        {
            get => _testNameFilter;
            set
            {
                if (_testNameFilter != value)
                {
                    _testNameFilter = value;
                    OnPropertyChanged();
                    Tests.Filter = item => FilterResults(item);
                }
            }
        }

        private static readonly string _allResultsFilter = TestExplorer.ResourceManager.GetString("TestExplorer_AllResults", CultureInfo.CurrentUICulture);
        private string _selectedOutcomeFilter = _allResultsFilter;
        public string SelectedOutcomeFilter
        {
            get => _selectedOutcomeFilter;
            set
            {
                if (_selectedOutcomeFilter != value)
                {
                    _selectedOutcomeFilter = value.Replace(" ", string.Empty);
                    OnPropertyChanged();
                    Tests.Filter = item => FilterResults(item);
                }
            }
        }

        private bool FilterResults(object unitTest)
        {
            OnPropertyChanged(nameof(Tests));

            var testMethodViewModel = unitTest as TestMethodViewModel;
            var memberName = testMethodViewModel.QualifiedName.MemberName;

            return memberName.ToUpper().Contains(TestNameFilter.ToUpper())
                && (SelectedOutcomeFilter.Equals(_allResultsFilter) || testMethodViewModel.Result.Outcome.ToString().Equals(_selectedOutcomeFilter));
        }

        private void HandleTestCompletion(object sender, TestCompletedEventArgs e)
        {
            if (TestGrouping != TestExplorerGrouping.Outcome)
            {
                return;
            }

            Tests.Refresh();
        }

        #region Commands

        public ReparseCommand RefreshCommand { get; set; }

        public RunAllTestsCommand RunAllTestsCommand { get; set; }
        public RepeatLastRunCommand RepeatLastRunCommand { get; set; }
        public RunNotExecutedTestsCommand RunNotExecutedTestsCommand { get; set; }
        // no way to run skipped tests. Those are skipped until reparsing anyways, so it's k
        public RunInconclusiveTestsCommand RunInconclusiveTestsCommand { get; set; }
        public RunFailedTestsCommand RunFailedTestsCommand { get; set; }
        public RunSucceededTestsCommand RunPassedTestsCommand { get; set; }
        public CommandBase RunSingleTestCommand { get; }
        public CommandBase RunSelectedTestsCommand { get; }
        public CommandBase RunSelectedGroupCommand { get; }

        public CommandBase CancelTestRunCommand { get; }
        public CommandBase ResetResultsCommand { get; }

        public AddTestModuleCommand AddTestModuleCommand { get; set; }
        public AddTestMethodCommand AddTestMethodCommand { get; set; }
        public AddTestMethodExpectedErrorCommand AddErrorTestMethodCommand { get; set; }

        public CommandBase CopyResultsCommand { get; }

        public CommandBase OpenTestSettingsCommand { get; }

        public INavigateCommand NavigateCommand { get; }

        public CommandBase CollapseAllCommand { get; }
        public CommandBase ExpandAllCommand { get; }

        #endregion

        #region Delegates

        private bool CanExecuteSingleTest(object obj)
        {
            return !Model.IsBusy && MouseOverTest != null;
        }

        private bool CanExecuteSelectedTestsCommand(object obj)
        {
            return !Model.IsBusy && obj is IList viewModels && viewModels.Count > 0;
        }

        private bool CanExecuteSelectedGroupCommand(object obj)
        {
            return !Model.IsBusy && (MouseOverTest != null || MouseOverGroup != null);
        }

        private bool CanExecuteResetResultsCommand(object obj)
        {
            return !Model.IsBusy && Tests.OfType<TestMethodViewModel>().Any(test => test.Result.Outcome != TestOutcome.Unknown);
        }

        private bool CanExecuteCancelTestRunCommand(object obj)
        {
            return Model.IsBusy;
        }

        private void ExecuteCollapseAll(object parameter)
        {
            ExpandedState = false;
        }

        private void ExecuteExpandAll(object parameter)
        {
            ExpandedState = true;
        }

        private void ExecuteSingleTestCommand(object obj)
        {
            if (MouseOverTest == null)
            {
                return;
            }

            Model.ExecuteTests(new List<TestMethodViewModel> { MouseOverTest });
        }

        private void ExecuteSelectedTestsCommand(object obj)
        {
            if (Model.IsBusy || !(obj is IList viewModels && viewModels.Count > 0))
            {
                return;
            }

            var models = viewModels.OfType<TestMethodViewModel>().ToList();

            if (!models.Any())
            {
                return;
            }

            Model.ExecuteTests(models);
        }

        private void ExecuteRunSelectedGroupCommand(object obj)
        {
            var tests = MouseOverTest is null
                ? MouseOverGroup
                : Tests.Groups.OfType<CollectionViewGroup>().FirstOrDefault(group => group.Items.Contains(MouseOverTest));

            if (tests is null)
            {
                return;
            }

            Model.ExecuteTests(tests.Items.OfType<TestMethodViewModel>().ToList());
        }

        private void ExecuteCancelTestRunCommand(object parameter)
        {
            Model.CancelTestRun();
        }

        private void ExecuteResetResultsCommand(object parameter)
        {
            foreach (var test in Tests.OfType<TestMethodViewModel>())
            {
                test.Result = new TestResult(TestOutcome.Unknown);
            }

            Tests.Refresh();
        }

        private void ExecuteCopyResultsCommand(object parameter)
        {
            const string XML_SPREADSHEET_DATA_FORMAT = "XML Spreadsheet";

            ColumnInfo[] columnInfos = { new ColumnInfo("Project"), new ColumnInfo("Component"), new ColumnInfo("Method"), new ColumnInfo("Outcome"), new ColumnInfo("Output"),
                                           new ColumnInfo("Start Time"), new ColumnInfo("End Time"), new ColumnInfo("Duration (ms)", hAlignment.Right) };

            // FIXME do that to the TestMethodViewModel
            var aResults = Model.Tests.Select(test => test.ToArray()).ToArray();

            var title = string.Format($"Rubberduck Test Results - {DateTime.Now.ToString(CultureInfo.InvariantCulture)}");

            //var textResults = title + Environment.NewLine + string.Join("", _results.Select(result => result.ToString() + Environment.NewLine).ToArray());
            var csvResults = ExportFormatter.Csv(aResults, title, columnInfos);
            var htmlResults = ExportFormatter.HtmlClipboardFragment(aResults, title, columnInfos);
            var rtfResults = ExportFormatter.RTF(aResults, title);

            using (var strm1 = ExportFormatter.XmlSpreadsheetNew(aResults, title, columnInfos))
            {
                //Add the formats from richest formatting to least formatting
                _clipboard.AppendStream(DataFormats.GetDataFormat(XML_SPREADSHEET_DATA_FORMAT).Name, strm1);
                _clipboard.AppendString(DataFormats.Rtf, rtfResults);
                _clipboard.AppendString(DataFormats.Html, htmlResults);
                _clipboard.AppendString(DataFormats.CommaSeparatedValue, csvResults);
                //_clipboard.AppendString(DataFormats.UnicodeText, textResults);

                _clipboard.Flush();
            }
        }

        // TODO - FIXME
        //KEEP THIS, AS IT MAKES FOR THE BASIS OF A USEFUL *SUMMARY* REPORT
        //private void ExecuteCopyResultsCommand(object parameter)
        //{
        //    var results = string.Join(Environment.NewLine, _model.LastRun.Select(test => test.ToString()));

        //    var passed = _model.LastRun.Count(test => test.Result.Outcome == TestOutcome.Succeeded) + " " + TestOutcome.Succeeded;
        //    var failed = _model.LastRun.Count(test => test.Result.Outcome == TestOutcome.Failed) + " " + TestOutcome.Failed;
        //    var inconclusive = _model.LastRun.Count(test => test.Result.Outcome == TestOutcome.Inconclusive) + " " + TestOutcome.Inconclusive;
        //    var ignored = _model.LastRun.Count(test => test.Result.Outcome == TestOutcome.Ignored) + " " + TestOutcome.Ignored;

        //    var duration = RubberduckUI.UnitTest_TotalDuration + " - " + TotalDuration;

        //    var resource = "Rubberduck Unit Tests - {0}{6}{1} | {2} | {3} | {4}{6}{5} ms{6}";
        //    var text = string.Format(resource, DateTime.Now, passed, failed, inconclusive, ignored, duration, Environment.NewLine) + results;

        //    _clipboard.Write(text);
        //}

        private void OpenSettings(object param)
        {
            using (var window = _settingsFormFactory.Create(SettingsViews.UnitTestSettings))
            {
                window.ShowDialog();
                _settingsFormFactory.Release(window);
            }
        }

        #endregion

        public void Dispose()
        {
            Model.TestCompleted -= HandleTestCompletion;
            Model.Dispose();
        }
    }
}
