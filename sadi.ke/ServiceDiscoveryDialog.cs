﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using IOInformatics.KE.PluginAPI;
using System.Collections;

namespace SADI.KEPlugin
{
    public partial class ServiceDiscoveryDialog : Form
    {
        private KEStore KE;
        private IEnumerable<IResource> SelectedNodes;
        private Hashtable Seen; 

        public ServiceDiscoveryDialog(KEStore ke, IEnumerable<IResource> selectedNodes)
        {
            KE = ke;
            SelectedNodes = selectedNodes;
            Seen = new Hashtable();
            InitializeComponent();
        }

        internal void FindServices()
        {
            FindServicesWorker.RunWorkerAsync();
            timer.Start();
        }

        private void FindServicesWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progress.Value = e.ProgressPercentage;
            if (e.UserState is SADIService && !Seen.Contains((e.UserState as SADIService).uri))
            {
                Seen.Add((e.UserState as SADIService).uri, Seen);
                ServiceSelectionControl control = new ServiceSelectionControl();
                control.setService(e.UserState as SADIService);
                ResizeLabels(control);
                flowLayoutPanel1.SuspendLayout();
                flowLayoutPanel1.Controls.Add(control);
                int i = 0;
                while ((i < flowLayoutPanel1.Controls.Count) && 
                    ((flowLayoutPanel1.Controls[i] as ServiceSelectionControl).service.name.CompareTo(control.service.name) < 0)) {
                        ++i;
                }
                flowLayoutPanel1.Controls.SetChildIndex(control, i);
                flowLayoutPanel1.ResumeLayout(false);
                flowLayoutPanel1.PerformLayout();
            }
            else if (e.UserState is string)
            {
                timer.Stop();
                status.Text = e.UserState as string;
                timer.Start();
            }
        }

        private void FindServicesWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            timer.Stop();
            progress.Visible = false;
            status.Text = "Found " + this.flowLayoutPanel1.Controls.Count + " services.";
        }

        private void invoke_Click(object sender, EventArgs e)
        {
            if (FindServicesWorker.IsBusy)
            {
                FindServicesWorker.CancelAsync();
            }

            List<SADIService> services = GetSelectedServices();
            if (services.Count > 0)
            {
                ServiceInvocationDialog dialog = new ServiceInvocationDialog(KE, services, SelectedNodes);
                dialog.Show();
                dialog.invokeServices();
                CleanUp();
            }
            else
            {
                MessageBox.Show(this,
                    "No services selected",
                    "Please select one or more services to invoke.",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private List<SADIService> GetSelectedServices()
        {
            List<SADIService> services = new List<SADIService>();
            foreach (Control control in flowLayoutPanel1.Controls)
            {
                if (control is ServiceSelectionControl)
                {
                    ServiceSelectionControl ssc = control as ServiceSelectionControl;
                    if (ssc.isSelected())
                    {
                        services.Add(ssc.service);
                    }
                }
            }
            return services;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            if (FindServicesWorker.IsBusy)
            {
                FindServicesWorker.CancelAsync();
            }
            CleanUp();
        }

        private static char[] ELLIPSIS = {'.'};
        private void timer_Tick(object sender, EventArgs e)
        {
            if (status.Text.EndsWith("..."))
            {
                status.Text = status.Text.TrimEnd(ELLIPSIS);
            }
            else
            {
                status.Text += ".";
            }
        }

        private void ServiceSelectionDialog_ResizeEnd(object sender, EventArgs e)
        {
            foreach (Control control in this.flowLayoutPanel1.Controls)
            {
                if (control is ServiceSelectionControl)
                {
                    ResizeLabels(control as ServiceSelectionControl);
                }
            }
        }

        private void ResizeLabels(ServiceSelectionControl control)
        {
            Padding padding = this.flowLayoutPanel1.Padding;
            control.sizeLabels(this.flowLayoutPanel1.Size.Width - padding.Left - padding.Right);
        }

        private void ServiceSelectionDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (FindServicesWorker.IsBusy)
            {
                FindServicesWorker.CancelAsync();
            }
        }

        private void CleanUp()
        {
            this.Hide();
            this.Dispose();
        }

        private const int SERVICES_PER_QUERY = 25;
        private void FindServicesWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            List<String> types = new List<String>();
            foreach (IResource node in SelectedNodes)
            {
                if (node is IEntity)
                {
                    if (FindServicesWorker.CancellationPending)
                    {
                        return;
                    }
                    else if (!KE.HasType(node))
                    {
                        Uri uri = (node as IEntity).Uri;
                        FindServicesWorker.ReportProgress(0, String.Format("Resolving {0}...", uri));
                        try
                        {
                            SADIHelper.debug("ServiceSelection", "resolving URI", node);
                            KE.Import(SemWebHelper.resolveURI(uri));
                        }
                        catch (Exception err)
                        {
                            SADIHelper.error("ServiceSelection", "error resolving URI", node, err);
                        }
                        try
                        {
                            SADIHelper.debug("ServiceSelection", "resolving against SADI resolver", node);
                            KE.Import(SADIHelper.resolve(uri));
                        }
                        catch (Exception err)
                        {
                            SADIHelper.error("ServiceSelection", "error resolving against SADI resolver", node, err);
                        }
                    }

                    foreach (IEntity type in KE.GetTypes(node as IEntity))
                    {
                        types.Add(type.Uri.ToString());
                    }
                }
            }

            //ICollection<SADIService> services = new List<SADIService>();
            //services.Add(new SADIService("http://sadiframework.org/examples/blast/human-blast", "NCBI BLAST (human)", "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada, dui eu tempus adipiscing, mauris sapien ultrices lectus, sit amet lobortis leo ipsum id lacus. Morbi porta, mi sit amet pulvinar adipiscing, leo tellus laoreet justo, et suscipit odio nisl nec velit. Pellentesque vel urna risus. Cras magna massa, volutpat at iaculis a, tincidunt et erat. Morbi ullamcorper feugiat augue, at pharetra nulla egestas at. Nunc ac orci ut erat porttitor auctor quis sit amet ipsum. Curabitur bibendum tortor quis libero adipiscing ultricies. Ut tempor rhoncus luctus."));
            //services.Add(new SADIService("http://sadiframework.org/examples/blast/mouse-blast", "NCBI BLAST (mouse)", "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada, dui eu tempus adipiscing, mauris sapien ultrices lectus, sit amet lobortis leo ipsum id lacus. Morbi porta, mi sit amet pulvinar adipiscing, leo tellus laoreet justo, et suscipit odio nisl nec velit. Pellentesque vel urna risus. Cras magna massa, volutpat at iaculis a, tincidunt et erat. Morbi ullamcorper feugiat augue, at pharetra nulla egestas at. Nunc ac orci ut erat porttitor auctor quis sit amet ipsum. Curabitur bibendum tortor quis libero adipiscing ultricies. Ut tempor rhoncus luctus."));

            // find services by exact input class; quick, but misses a lot...
            FindServicesWorker.ReportProgress(0, "Finding services by direct type...");
            ICollection<SADIService> services = SADIRegistry.Instance().findServicesByInputClass(types);
            int i = 0;
            int n = services.Count;
            foreach (SADIService service in services)
            {
                if (FindServicesWorker.CancellationPending)
                {
                    return;
                }
                else
                {
                    SADIRegistry.Instance().addPropertyRestrictions(service);
                    FindServicesWorker.ReportProgress((++i * 100) / n, service);
                }
            }

            // reset progress bar
            FindServicesWorker.ReportProgress(1, "Finding services by input instance query...");

            // find service by input instance SPARQL query; slow, but is complete modulo reasoning...
            i = 0;
            n = SADIRegistry.Instance().getServiceCount();
            do
            {
                if (FindServicesWorker.CancellationPending)
                {
                    return;
                }
                else
                {
                    services = SADIRegistry.Instance().getAllServices(i, SERVICES_PER_QUERY);
                    foreach (SADIService service in services)
                    {
                        if (FindServicesWorker.CancellationPending)
                        {
                            return;
                        }
                        else if (checkForInputInstances(service, SelectedNodes))
                        {
                            SADIRegistry.Instance().addPropertyRestrictions(service);
                            FindServicesWorker.ReportProgress((++i * 100) / n, service);
                        }
                    }
                }
            } while (services.Count == SERVICES_PER_QUERY);
        }

        private bool checkForInputInstances(SADIService service, IEnumerable<IResource> selectedNodes)
        {
            if (service.inputInstanceQuery != null)
            {
                string query = SADIHelper.convertConstructQuery(service.inputInstanceQuery);
                if (query != null)
                {
                    foreach (ISPARQLResult result in KE.Graph.Query(query).Results)
                    {
                        foreach (IResource node in selectedNodes)
                        {
                            if (node.Equals(result["input"]))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}