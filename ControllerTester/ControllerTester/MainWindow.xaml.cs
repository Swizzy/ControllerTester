//
// MainWindow.xaml.cs
// ControllerTester
//
// Created by Swizzy 15/08/2015
// Copyright (c) 2015 Swizzy. All rights reserved.

using System.Reflection;

namespace ControllerTester {

    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();
            SetTitle();
        }

        private void SetTitle() {
            var ass = Assembly.GetAssembly(typeof(MainWindow));
            var ver = ass.GetName().Version;
            var name = "";
            var attributes = ass.GetCustomAttributes(typeof(AssemblyCompanyAttribute), true);
            foreach(var attribute in attributes) {
                name = ((AssemblyCompanyAttribute)attribute).Company;
                break;
            }
            if(string.IsNullOrWhiteSpace(name))
                name = "Swizzy"; // Default to my name if there is nothing specified in the attributes
            Title = string.Format(Title, ver.Major, ver.Minor, ver.Build, name);
        }
    }

}