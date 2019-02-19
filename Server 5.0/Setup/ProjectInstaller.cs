using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;


namespace RosService
{
    [RunInstaller(false)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        public override object InitializeLifetimeService()
        {
            return base.InitializeLifetimeService();
        }
    }
}
