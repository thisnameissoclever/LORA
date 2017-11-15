using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using Twilio;
using Twilio.Http;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using System.Threading.Tasks;
using System.Threading;

namespace LORA
{
    public partial class Lora : Form
    {
        // Gets the Configuration file
        string appDirectory = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Config.txt");
        private BackgroundWorker bc_worker = new BackgroundWorker();
        private BackgroundWorker bc_Simultaniousworker;

        bool isRepeatRunning = false;
        bool canStopCall_inBtwn = false;
        int worker_Sno = 1;
        public Lora()
        {
            InitializeComponent();

            // Loading the availeble SIP Credentials
            LoadSIPCredentials();

            // Initilizing the Background worker process
            // For initial calling
            bc_worker.WorkerReportsProgress = true;
            bc_worker.WorkerSupportsCancellation = true;
            bc_worker.ProgressChanged += bc_worker_ProgressChanged;
            bc_worker.DoWork += bc_worker_DoWork;
            bc_worker.RunWorkerCompleted += bc_worker_RunWorkerCompleted;
        }

        #region Textbox Key Press Events
        // Target number Key Press event which allows only numbers, backspace button, '+' and Ctrl Key
        private void txtTargetNumber_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!(Char.IsDigit(e.KeyChar) || (e.KeyChar == (char)Keys.Back) || (e.KeyChar == (char)Keys.ControlKey)))
                e.Handled = true;

            if (e.KeyChar == '+' || e.KeyChar == '-' || e.KeyChar == '(' || e.KeyChar == ')' || e.KeyChar == ' ' || e.KeyChar == '.')
                e.Handled = false;
        }

        // Target number Key down event which allows only Ctrl + A, Ctrl + C, Ctrl + V and Ctrl + X
        private void txtTargetNumber_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                if (sender != null)
                    ((TextBox)sender).SelectAll();
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                if (sender != null)
                    ((TextBox)sender).Copy();
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                if (sender != null)
                    ((TextBox)sender).Paste();
            }
            else if (e.Control && e.KeyCode == Keys.X)
            {
                if (sender != null)
                    ((TextBox)sender).Cut();
            }
        }

        // specical charecters will be removed from the Target number
        private void txtTargetNumber_CursorChanged(object sender, EventArgs e)
        {
            char[] specialchars = new char[] { '(', ')', '-', ' ', '.' };
            txtTargetNumber.Text = string.Concat(txtTargetNumber.Text.Split(specialchars, StringSplitOptions.RemoveEmptyEntries));
        }

        // Dail times Key Press event which allows only numbers and backspace button
        private void txtdailNo_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!(Char.IsDigit(e.KeyChar) || (e.KeyChar == (char)Keys.Back)))
                e.Handled = true;
        }

        // Redail Duratioin Key Press event which allows only numbers and backspace button
        private void txttimeDuration_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!(Char.IsDigit(e.KeyChar) || (e.KeyChar == (char)Keys.Back)))
                e.Handled = true;
        }

        // specical charecters will be removed from the Target number
        private void txtTargetNumber_Leave(object sender, EventArgs e)
        {
            char[] specialchars = new char[] { '(', ')', '-', ' ', '.' };
            txtTargetNumber.Text = string.Concat(txtTargetNumber.Text.Split(specialchars, StringSplitOptions.RemoveEmptyEntries));
        }
        #endregion

        #region Radio button Events
        // Radio button mp3 checked event
        private void rdbtnmp3_CheckedChanged(object sender, EventArgs e)
        {
            //txtVoiceText.Text = string.Empty;
            txtVoiceText.Visible = false;
            txtaudiofile.Visible = true;
            lblaudiolbl.Text = "URL of audio .mp3 or TTS .XML :";

            chkboxdisconntVoicemail.Location = new Point(13, 208);
        }

        // Radio button voice text checked event
        private void rdbtnvoiceText_CheckedChanged(object sender, EventArgs e)
        {
            //txtaudiofile.Text = string.Empty;
            txtaudiofile.Visible = false;
            txtVoiceText.Visible = true;
            lblaudiolbl.Text = "Audio to play (Voice Text) :";

            chkboxdisconntVoicemail.Location = new Point(13, 266);
        }
        #endregion

        #region Click Events

        // Click on help rediects to http://babysealclubbing.club/
        private void picboxHelp_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://babysealclubbing.club/");
        }

        // link website button which redirects to http://babysealclubbing.club/
        private void lnklblWebsite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://babysealclubbing.club/");
        }

        // Click event to save the credentials in json format
        private void btnTwiliSave_Click(object sender, EventArgs e)
        {
            try
            {
                lblStart_stopmsg.Visible = false;
                if (string.IsNullOrEmpty(txtAccountSID.Text))
                {
                    lblTwilliCreMsge.Text = "Account SID is required";
                    lblTwilliCreMsge.ForeColor = Color.FromArgb(255, 128, 0); ;
                    lblTwilliCreMsge.Visible = true;
                }
                else if (string.IsNullOrEmpty(txtAuthToken.Text))
                {
                    lblTwilliCreMsge.Text = "Auth Token is required";
                    lblTwilliCreMsge.ForeColor = Color.FromArgb(255, 128, 0); ;
                    lblTwilliCreMsge.Visible = true;
                }
                else
                {
                    Credentials crd = new Credentials();
                    crd.Account_SID = txtAccountSID.Text.Trim();
                    crd.Auth_Token = txtAuthToken.Text.Trim();
                    crd.Dail_Times = txtdailNo.Text.Trim();
                    crd.Redail = txttimeDuration.Text.Trim();
                    crd.mp3_Link = txtaudiofile.Text.Trim();
                    crd.VoiceText = txtVoiceText.Text.Trim();

                    string ConfigJson = JsonConvert.SerializeObject(crd);

                    //write string to file
                    if (!File.Exists(appDirectory))
                    {
                        using (StreamWriter sw = (File.Exists(appDirectory)) ? File.AppendText(appDirectory) : File.CreateText(appDirectory))
                        {
                            sw.WriteLine(ConfigJson);
                        }

                        lblTwilliCreMsge.Text = "Settings and credentials saved successfully";
                        lblTwilliCreMsge.ForeColor = System.Drawing.Color.YellowGreen;
                        lblTwilliCreMsge.Visible = true;
                    }
                    else
                    {
                        // Update the config file with new data
                        File.WriteAllText(appDirectory, ConfigJson);

                        lblTwilliCreMsge.Text = "Settings and credentials updated successfully";
                        lblTwilliCreMsge.ForeColor = System.Drawing.Color.YellowGreen;
                        lblTwilliCreMsge.Visible = true;
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        // Click event to clear all the Result fields
        private void lnkClear_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            txtEventlog.Text = string.Empty;
            txtEventlog.SelectionStart = txtEventlog.Text.Length;
            txtEventlog.ScrollToCaret();
        }

        // Start and stop the Call app
        private void btnStart_Stop_Click(object sender, EventArgs e)
        {
            try
            {
                lblTwilliCreMsge.Visible = false;
                lblNoOfCallerIds.Text = "0";
                lblNoOfRun.Text = "0";
                lblSuccessNo.Text = "0";
                lblnoFail.Text = "0";
                canStopCall_inBtwn = false;

                if (btnStart_Stop.Text == "Suppressing fire!")
                {
                    #region Validations
                    if (string.IsNullOrEmpty(txtTargetNumber.Text))
                    {
                        lblStart_stopmsg.Text = "Target Number is required";
                        lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                        lblStart_stopmsg.Visible = true;
                    }
                    else if (txtTargetNumber.Text.Count() < 12)
                    {
                        lblStart_stopmsg.Text = "Target is not a valid phone number";
                        lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                        lblStart_stopmsg.Visible = true;
                    }
                    #endregion
                    else if (chkRepeatDail.Checked)
                    {
                        if (string.IsNullOrEmpty(txttimeDuration.Text))
                        {
                            lblStart_stopmsg.Text = "Time in seconds are required";
                            lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                            lblStart_stopmsg.Visible = true;
                        }
                        else if (Convert.ToInt32(txttimeDuration.Text) == 0)
                        {
                            lblStart_stopmsg.Text = "Time must be greater than zero";
                            lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                            lblStart_stopmsg.Visible = true;
                        }
                        else
                        {
                            // General Validations
                            bool result = Validation();
                            if (result)
                            {
                                lblStart_stopmsg.Visible = false;
                                btnStart_Stop.Text = "Stop";
                                btnStart_Stop.BackColor = Color.FromArgb(255, 128, 128);
                                EnableElements(false);

                                // calling background worker for multiple calling purpose in asynchronous.
                                bc_worker.RunWorkerAsync("Multiple");
                            }
                        }
                    }
                    else
                    {
                        // General Validations
                        bool result = Validation();
                        if (result)
                        {
                            lblStart_stopmsg.Visible = false;
                            btnStart_Stop.Text = "Stop";
                            btnStart_Stop.BackColor = Color.FromArgb(255, 128, 128);
                            EnableElements(false);

                            if (!bc_worker.IsBusy)
                            {
                                // calling background worker for single calling in asynchronous.
                                bc_worker.RunWorkerAsync("Single");
                            }
                            else
                            {
                                lblStart_stopmsg.Text = "Tool is busy with previous operations. Please try again after some time.";
                                isRepeatRunning = false;
                                canStopCall_inBtwn = true;
                                lblStart_stopmsg.Visible = true;
                                btnStart_Stop.Text = "Suppressing fire!";
                                btnStart_Stop.BackColor = Color.FromArgb(59, 59, 59);

                                // Enable and siable all the elements
                                EnableElements(true);
                            }
                        }
                    }
                }
                else
                {
                    isRepeatRunning = false;
                    canStopCall_inBtwn = true;
                    bc_worker.CancelAsync();
                    bc_worker.Dispose();
                    if (chkRepeatDail.Checked)
                    {
                        bc_Simultaniousworker.CancelAsync();
                        bc_Simultaniousworker.Dispose();
                    }
                    btnStart_Stop.Text = "Suppressing fire!";
                    btnStart_Stop.BackColor = Color.FromArgb(59, 59, 59);

                    // Enable and siable all the elements
                    EnableElements(true);
                }
            }
            catch (Exception ex)
            {
                isRepeatRunning = false;
                canStopCall_inBtwn = true;
                bc_worker.CancelAsync();
                bc_worker.Dispose();
                if (chkRepeatDail.Checked)
                {
                    bc_Simultaniousworker.CancelAsync();
                    bc_Simultaniousworker.Dispose();
                }
                btnStart_Stop.Text = "Suppressing fire!";
                btnStart_Stop.BackColor = Color.FromArgb(59, 59, 59);

                // Enable and siable all the elements
                EnableElements(true);
                if (ex.Message == "This BackgroundWorker is currently busy and cannot run multiple tasks concurrently.")
                {
                    lblStart_stopmsg.Text = "Please wait...";
                }
                else
                {
                    lblStart_stopmsg.Text = ex.Message;
                }
                lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                lblStart_stopmsg.Visible = true;
            }
        }

        #endregion

        #region Background Process

        // This event handler Starts the work --Ashok Kumar Vemulapalli
        private void bc_worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // run all background tasks here
                if (bc_worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                else
                {
                    // Perform a time consuming operation and report progress.
                    if (bc_worker.CancellationPending)
                    {
                        e.Cancel = true;
                    }
                    else
                    {
                        if (e.Argument.ToString() == "Single")
                        {
                            bc_worker.ReportProgress(worker_Sno, "LORA started");
                            Initializing_SIPCredentials("Single");
                            bc_worker.CancelAsync();
                            bc_worker.Dispose();
                            e.Result = "Success";
                            //e.Cancel = true;
                        }
                        else if (e.Argument.ToString() == "Multiple")
                        {
                            bc_worker.ReportProgress(worker_Sno, "LORA started");
                            Initializing_SIPCredentials("Multiple");
                            bc_worker.CancelAsync();
                            bc_worker.Dispose();
                            e.Result = "Success";
                        }
                        else
                        {
                            // Do something
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        // This event handler updates the progress --Ashok Kumar Vemulapalli
        private void bc_worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState.ToString().Contains("CallerIdsCount"))
            {
                string[] callercount = e.UserState.ToString().Split('-');
                lblNoOfCallerIds.Text = callercount[1];
            }
            else if (e.UserState.ToString().Contains("Runed"))
            {
                string[] runcount = e.UserState.ToString().Split('-');
                int runtime = Convert.ToInt32(lblNoOfRun.Text);
                lblNoOfRun.Text = runcount[1] == "Yes" ? (runtime + 1).ToString() : runtime.ToString();
            }
            else if (e.UserState.ToString().Contains("SuccessCount"))
            {
                string[] successcount = e.UserState.ToString().Split('-');
                if (successcount[1] == "Yes")
                {
                    int success = Convert.ToInt32(lblSuccessNo.Text);
                    lblSuccessNo.Text = (success + 1).ToString();
                }
                else
                {
                    int failure = Convert.ToInt32(lblnoFail.Text);
                    lblnoFail.Text = (failure + 1).ToString();
                }
            }
            else if (e.UserState.ToString() == "Success")
            {
                lblStart_stopmsg.Text = "Completed";
                lblStart_stopmsg.ForeColor = System.Drawing.Color.YellowGreen;
                lblStart_stopmsg.Visible = true;

                btnStart_Stop.Text = "Suppressing fire!";
                btnStart_Stop.BackColor = Color.FromArgb(59, 59, 59);
                EnableElements(true);
            }
            else
            {
                txtEventlog.Text += e.UserState.ToString() + " \r\n";
                txtEventlog.SelectionStart = txtEventlog.Text.Length;
                txtEventlog.ScrollToCaret();
            }

            worker_Sno = worker_Sno + 1;
        }

        // This event handler fires when the work sompletes all the async process --Ashok Kumar Vemulapalli
        private void bc_worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                txtEventlog.Text += "Canceled! \r\n";
                txtEventlog.SelectionStart = txtEventlog.Text.Length;
                txtEventlog.ScrollToCaret();
            }
            else if (e.Error != null)
            {
                txtEventlog.Text += "Error: " + e.Error.Message + "\r\n";
                txtEventlog.SelectionStart = txtEventlog.Text.Length;
                txtEventlog.ScrollToCaret();
            }
            else
            {
                if (e.Result.ToString() == "Success")
                {
                    lblStart_stopmsg.Text = "Completed";
                    lblStart_stopmsg.ForeColor = System.Drawing.Color.YellowGreen;
                    lblStart_stopmsg.Visible = true;

                    btnStart_Stop.Text = "Suppressing fire!";
                    btnStart_Stop.BackColor = Color.FromArgb(59, 59, 59);
                    EnableElements(true);
                }
                else
                {
                }
            }
            //btnStart_Stop.Enabled = true;
        }

        // This event handler Starts Simultanious calling process --Ashok Kumar Vemulapalli
        private void bc_Simultaniousworker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Argument.ToString()))
                {
                    Make_A_Call(e.Argument.ToString());
                    e.Result = "Success";
                }
                else
                {
                    // Do something
                }
            }
            catch (Exception ex)
            {

            }
        }

        #endregion

        #region Private Methods
        // Loads the SIP Credentials from Configration File --Ashok Kumar Vemulapalli
        private void LoadSIPCredentials()
        {
            try
            {
                if (File.Exists(appDirectory))
                {
                    string Jsoncontent = File.ReadAllText(appDirectory);
                    JToken token = JObject.Parse(Jsoncontent);
                    txtAccountSID.Text = token["Account_SID"].ToString();
                    txtAuthToken.Text = token["Auth_Token"].ToString();
                    txtdailNo.Text = token["Dail_Times"].ToString();
                    txttimeDuration.Text = token["Redail"].ToString();
                    txtaudiofile.Text = token["mp3_Link"].ToString();
                    txtVoiceText.Text = token["VoiceText"].ToString();
                }
                else
                {
                    // do something
                }
            }
            catch (Exception ex)
            {
            }
        }

        // Enabling and Disabling the Elemets accordingly -- Ashok Kumar Vemulapalli
        private void EnableElements(bool isEnable)
        {
            txtTargetNumber.Enabled = isEnable;
            chkRepeatDail.Enabled = isEnable;
            txtdailNo.Enabled = isEnable;
            txttimeDuration.Enabled = isEnable;
            rdbtnmp3.Enabled = isEnable;
            rdbtnvoiceText.Enabled = isEnable;
            txtaudiofile.Enabled = isEnable;
            txtVoiceText.Enabled = isEnable;
            chkboxdisconntVoicemail.Enabled = isEnable;
            txtAccountSID.Enabled = isEnable;
            txtAuthToken.Enabled = isEnable;
            btnTwiliSave.Enabled = isEnable;
        }

        // General Validations -- Ashok Kumar Vemulapalli
        private bool Validation()
        {
            bool IsValid = false;
            if (rdbtnmp3.Checked && string.IsNullOrEmpty(txtaudiofile.Text))
            {
                lblStart_stopmsg.Text = "xml link with Audio file(.mp3) URL is required";
                lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                lblStart_stopmsg.Visible = true;
                //else if (Path.GetExtension(txtaudiofile.Text) != ".mp3")
            }

            else if (rdbtnvoiceText.Checked && string.IsNullOrEmpty(txtVoiceText.Text))
            {
                lblStart_stopmsg.Text = "Enter text that to be voiced.";
                lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                lblStart_stopmsg.Visible = true;
            }

            else if (string.IsNullOrEmpty(txtAccountSID.Text))
            {
                lblStart_stopmsg.Text = "Account SID is required";
                lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                lblStart_stopmsg.Visible = true;
            }
            else if (string.IsNullOrEmpty(txtAuthToken.Text))
            {
                lblStart_stopmsg.Text = "Auth Token is required";
                lblStart_stopmsg.ForeColor = Color.FromArgb(255, 128, 0); ;
                lblStart_stopmsg.Visible = true;
            }
            else
            {
                IsValid = true;
            }
            return IsValid;
        }

        // Call Functionality -- Uncomment the calling part accordingly -- Ashok Kumar Vemulapalli
        private void Initializing_SIPCredentials(string CallType)
        {
            // Getting all my outgoing caller Ids from SIP
            TwilioClient.Init(txtAccountSID.Text.Trim(), txtAuthToken.Text.Trim());

            bc_worker.ReportProgress(worker_Sno, "Verifying SIP credentials and requesting your incoming caller Ids.");

            //var callerIds = OutgoingCallerIdResource.Read();
            var callerIds = IncomingPhoneNumberResource.Read();

            bc_worker.ReportProgress(worker_Sno, "Authentication successful. Building CID list.");

            //listing
            List<CallerIds> lstcalerIds = new List<CallerIds>();
            DateTime presntTime = DateTime.Now;

            foreach (var callerId in callerIds)
            {
                CallerIds cid = new CallerIds();
                cid.AccountSId = callerId.AccountSid;
                cid.FriendlyName = callerId.FriendlyName;
                cid.PhoneNumber = callerId.PhoneNumber.ToString();
                cid.CallTime = chkRepeatDail.Checked ? presntTime : DateTime.Now;
                lstcalerIds.Add(cid);

                presntTime = presntTime.AddSeconds(Convert.ToInt32(txttimeDuration.Text));
            }

            bc_worker.ReportProgress(worker_Sno, "CallerIdsCount-" + lstcalerIds.Count.ToString());

            if (CallType == "Single")
            {
                // Picking random number
                CallerIds randomcid = lstcalerIds.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                bc_worker.ReportProgress(worker_Sno, "Dailing to target number: " + txtTargetNumber.Text.Trim() + " from Source: " + randomcid.PhoneNumber);

                Make_A_Call(randomcid.PhoneNumber);
            }
            else if (CallType == "Multiple")
            {
                MultipleTimeCall(lstcalerIds);
            }
        }

        // To make multiple calls and Repeat functionality simultaniously -- Ashok Kumar Vemulapalli
        private void MultipleTimeCall(List<CallerIds> lstcalerIds)
        {
            try
            {
                int noOfDailTimes = !string.IsNullOrEmpty(txtdailNo.Text) ? Convert.ToInt32(txtdailNo.Text) : 0;
                int timeIntraval = Convert.ToInt32(txttimeDuration.Text);
                int callerCount = lstcalerIds.Count() - 1;
                int prsntCalId = 0;

                isRepeatRunning = true;

                #region Old Code Commented by Ashok 03 sep 2017
                // Adding each caller phone numbers in making a call parallely
                //Parallel.ForEach(lstcalerIds, x =>
                //{
                //    Make_A_Call_Simulatanious(x.PhoneNumber, noOfDailTimes, duration, current);
                //});
                #endregion

                if (noOfDailTimes != 0)
                {
                    // future time will be max 24 hours + of total cal time
                    DateTime future = DateTime.Now.AddSeconds((noOfDailTimes * timeIntraval) + 86400);
                    int dailCompleted = 0;
                    while (future > DateTime.Now)
                    {
                        if (isRepeatRunning)
                        {
                            if (dailCompleted < noOfDailTimes)
                            {
                                if (lstcalerIds[prsntCalId].CallTime <= DateTime.Now)
                                {
                                    bc_Simultaniousworker = new BackgroundWorker();
                                    bc_Simultaniousworker.WorkerReportsProgress = true;
                                    bc_Simultaniousworker.WorkerSupportsCancellation = true;
                                    bc_Simultaniousworker.DoWork += new DoWorkEventHandler(bc_Simultaniousworker_DoWork);
                                    bc_Simultaniousworker.ProgressChanged += new ProgressChangedEventHandler(bc_worker_ProgressChanged);

                                    bc_Simultaniousworker.RunWorkerAsync(lstcalerIds[prsntCalId].PhoneNumber);

                                    foreach (CallerIds cid in lstcalerIds.Where(r => r.PhoneNumber == lstcalerIds[prsntCalId].PhoneNumber))
                                    {
                                        cid.CallTime = lstcalerIds[prsntCalId].CallTime.AddSeconds(timeIntraval);
                                    }
                                    prsntCalId = callerCount == prsntCalId ? 0 : prsntCalId + 1;
                                    dailCompleted = dailCompleted + 1;

                                    if (dailCompleted == noOfDailTimes)
                                    {
                                        Thread.Sleep(dailCompleted * 5 * 1000);
                                    }
                                }
                            }
                            else
                            {
                                isRepeatRunning = false;
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    // future time will be max 24 hours + of total cal time
                    DateTime future = DateTime.Now.AddSeconds(86400 * timeIntraval);
                    while (future > DateTime.Now)
                    {
                        if (isRepeatRunning)
                        {
                            if (lstcalerIds[prsntCalId].CallTime <= DateTime.Now)
                            {
                                BackgroundWorker bc_Simultaniousworker = new BackgroundWorker();
                                bc_Simultaniousworker.WorkerReportsProgress = true;
                                bc_Simultaniousworker.WorkerSupportsCancellation = true;
                                bc_Simultaniousworker.DoWork += new DoWorkEventHandler(bc_Simultaniousworker_DoWork);
                                bc_Simultaniousworker.RunWorkerAsync(lstcalerIds[prsntCalId].PhoneNumber);

                                foreach (CallerIds cid in lstcalerIds.Where(r => r.PhoneNumber == lstcalerIds[prsntCalId].PhoneNumber))
                                {
                                    cid.CallTime = lstcalerIds[prsntCalId].CallTime.AddSeconds(timeIntraval);
                                }
                                prsntCalId = callerCount == prsntCalId ? 0 : prsntCalId + 1;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                bc_worker.ReportProgress(worker_Sno, "Call failed! Error message : " + ex.Message);
            }
        }

        // Function that makes a call and return status -- Ashok Kumar Vemulapalli
        private void Make_A_Call(string PhoneNumber)
        {
            try
            {
                bc_worker.ReportProgress(worker_Sno, "Spoofing a random phone number, from the ones available.");

                // Picking random number
                bc_worker.ReportProgress(worker_Sno, "Dailing to target number: " + txtTargetNumber.Text.Trim() + " from Source: " + PhoneNumber);

                //TwilioClient.Init(randomcid.AccountSId, txtAuthToken.Text.Trim());
                var statusCallbackEvents = new List<string>() { "initiated", "ringing", "answered", "completed" };

                Uri callerURI;
                bc_worker.ReportProgress(worker_Sno, "Call initializing...");

                if (rdbtnmp3.Checked)
                {
                    callerURI = new Uri(txtaudiofile.Text.Trim());
                }
                else if (rdbtnvoiceText.Checked)
                {
                    string _burl = string.Format("{0}{1}", "http://twimlets.com/message?Message=", txtVoiceText.Text.Trim());
                    callerURI = new Uri(_burl);
                }
                else
                {
                    //Do something Default
                    callerURI = new Uri(txtaudiofile.Text.Trim());
                }

                // Makes a call to the target number
                var call = CallResource.Create(new PhoneNumber(txtTargetNumber.Text.Trim()),
                                               new PhoneNumber(PhoneNumber),
                                               machineDetection: "DetectMessageEnd",
                                               machineDetectionTimeout: 20,
                                               url: callerURI,
                                               statusCallbackMethod: new HttpMethod("POST"),
                                               statusCallbackEvent: statusCallbackEvents);

                // initial response
                bool canstopResponse; string currentStatus;

                CallResponseLog(call.Status.ToString(), out canstopResponse, out currentStatus);
                //CallResponseLog("queued", out canstopResponse, out currentStatus); // Testing uncomment after testing

                #region Response Status
                // looping int.max times
                for (int i = 0; i <= int.MaxValue; i++)
                {
                    //var response = CallResource.Fetch("CA7575d13b16c2997098e6b1138a46cf60"); // Testing uncomment after testing
                    var response = CallResource.Fetch(call.Sid);
                    if (response.Status.ToString() == "queued" || response.Status.ToString() == "ringing" || response.Status.ToString() == "in-progress")
                    {
                        if (canStopCall_inBtwn)
                        {
                            // Uncomment after testing
                            //CallResource.Update("CA7575d13b16c2997098e6b1138a46cf60", status: CallResource.UpdateStatusEnum.Completed); // Testing uncomment after testing
                            var stopresponse = CallResource.Update(call.Sid, status: CallResource.UpdateStatusEnum.Completed);
                            bc_worker.ReportProgress(worker_Sno, "Call terminated by user");
                            break;
                        }
                        else
                        {
                            if (!canstopResponse)
                            {
                                if (currentStatus != response.Status.ToString())
                                    CallResponseLog(response.Status.ToString(), out canstopResponse, out currentStatus);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        CallResponseLog(response.Status.ToString(), out canstopResponse, out currentStatus);
                        break;
                    }
                }
                #endregion

                canStopCall_inBtwn = false;
                bc_worker.ReportProgress(worker_Sno, "Runed-Yes");
            }
            catch (Exception ex)
            {
                bc_worker.ReportProgress(worker_Sno, "Call failed! Error message : " + ex.Message);
                isRepeatRunning = false;
            }
        }

        // Based on call Response Event log Displays -- Ashok Kumar Vemulapalli 
        private void CallResponseLog(string newStatus, out bool canstoplooping, out string currentStatus)
        {
            currentStatus = newStatus;
            switch (newStatus)
            {
                case "queued":
                    bc_worker.ReportProgress(worker_Sno, "Call queued...");
                    canstoplooping = false;
                    break;

                case "ringing":
                    bc_worker.ReportProgress(worker_Sno, "Ringing...");
                    canstoplooping = false;
                    break;

                case "in-progress":
                    if (rdbtnvoiceText.Checked)
                    {
                        bc_worker.ReportProgress(worker_Sno, "Call with " + txtTargetNumber.Text.Trim() + " connected and in-progress." + Environment.NewLine + "Playing text-to-speech message.");
                    }
                    else
                    {
                        bc_worker.ReportProgress(worker_Sno, "Call with " + txtTargetNumber.Text.Trim() + " connected and in-progress." + Environment.NewLine + "Playing .mp3 file from " + txtaudiofile.Text.Trim());
                    }
                    canstoplooping = false;
                    break;

                case "canceled":
                    bc_worker.ReportProgress(worker_Sno, "Call cancelled");
                    bc_worker.ReportProgress(worker_Sno, "SuccessCount-No");
                    canstoplooping = true;
                    break;

                case "completed":
                    bc_worker.ReportProgress(worker_Sno, "Call completed successfully");
                    bc_worker.ReportProgress(worker_Sno, "SuccessCount-Yes");
                    canstoplooping = true;
                    break;

                case "busy":
                    bc_worker.ReportProgress(worker_Sno, "Target number is busy.");
                    bc_worker.ReportProgress(worker_Sno, "SuccessCount-No");
                    canstoplooping = true;
                    break;

                case "failed":
                    bc_worker.ReportProgress(worker_Sno, "Call failed");
                    bc_worker.ReportProgress(worker_Sno, "SuccessCount-No");
                    canstoplooping = true;
                    break;

                case "no-answer":
                    bc_worker.ReportProgress(worker_Sno, "No answer from Target number");
                    bc_worker.ReportProgress(worker_Sno, "SuccessCount-No");
                    canstoplooping = true;
                    break;

                default:
                    bc_worker.ReportProgress(worker_Sno, "Call failed");
                    bc_worker.ReportProgress(worker_Sno, "SuccessCount-No");
                    canstoplooping = true;
                    break;
            }
        }

        // Function that makes a call and return status -- Ashok Kumar Vemulapalli -- Not Using old function
        private void Make_A_Call(List<CallerIds> lstcalerIds)
        {
            try
            {
                bc_worker.ReportProgress(worker_Sno, "Spoofing a random phone number, from the ones available.");

                // Picking random number
                CallerIds randomcid = lstcalerIds.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                bc_worker.ReportProgress(worker_Sno, "Dailing to target number: " + txtTargetNumber.Text.Trim() + " from Source: " + randomcid.PhoneNumber);

                //TwilioClient.Init(randomcid.AccountSId, txtAuthToken.Text.Trim());
                var statusCallbackEvents = new List<string>() { "initiated", "ringing", "answered", "completed" };

                Uri callerURI;
                bc_worker.ReportProgress(worker_Sno, "Call initializing...");

                if (rdbtnmp3.Checked)
                {
                    callerURI = new Uri(txtaudiofile.Text.Trim());
                }
                else if (rdbtnvoiceText.Checked)
                {
                    string _burl = string.Format("{0}{1}", "http://twimlets.com/message?Message=", txtVoiceText.Text.Trim());
                    callerURI = new Uri(_burl);
                }
                else
                {
                    //Do something Default
                    callerURI = new Uri(txtaudiofile.Text.Trim());
                }

                // Makes a call to the target number
                var call = CallResource.Create(new PhoneNumber(txtTargetNumber.Text.Trim()),
                                               new PhoneNumber(randomcid.PhoneNumber),
                                               machineDetection: "DetectMessageEnd",
                                               machineDetectionTimeout: 20,
                                               url: callerURI,
                                               statusCallbackMethod: new HttpMethod("POST"),
                                               statusCallbackEvent: statusCallbackEvents);

                // initial response
                bool canstopResponse; string currentStatus;

                CallResponseLog(call.Status.ToString(), out canstopResponse, out currentStatus);
                //CallResponseLog("queued", out canstopResponse, out currentStatus); // Testing uncomment after testing

                #region Response Status
                // looping int.max times
                for (int i = 0; i <= int.MaxValue; i++)
                {
                    //var response = CallResource.Fetch("CA7575d13b16c2997098e6b1138a46cf60"); // Testing uncomment after testing
                    var response = CallResource.Fetch(call.Sid);
                    if (response.Status.ToString() == "queued" || response.Status.ToString() == "ringing" || response.Status.ToString() == "in-progress")
                    {
                        if (canStopCall_inBtwn)
                        {
                            // Uncooment after testing
                            //CallResource.Update("CA7575d13b16c2997098e6b1138a46cf60", status: CallResource.UpdateStatusEnum.Completed);
                            var stopresponse = CallResource.Update(call.Sid, status: CallResource.UpdateStatusEnum.Completed);
                            bc_worker.ReportProgress(worker_Sno, "Call terminated by user");
                            break;
                        }
                        else
                        {
                            if (!canstopResponse)
                            {
                                if (currentStatus != response.Status.ToString())
                                    CallResponseLog(response.Status.ToString(), out canstopResponse, out currentStatus);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        CallResponseLog(response.Status.ToString(), out canstopResponse, out currentStatus);
                        break;
                    }
                }
                #endregion

                canStopCall_inBtwn = false;
                bc_worker.ReportProgress(worker_Sno, "Runed-Yes");
            }
            catch (Exception ex)
            {
                bc_worker.ReportProgress(worker_Sno, "Call failed! Error message : " + ex.Message);
            }
        }

		#endregion
		
	}

	class Credentials
    {
        public string Account_SID { get; set; }
        public string Auth_Token { get; set; }
        public string Dail_Times { get; set; }
        public string Redail { get; set; }
        public string mp3_Link { get; set; }
        public string VoiceText { get; set; }

    }

    class CallerIds
    {
        public string AccountSId { get; set; }
        public string FriendlyName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime CallTime { get; set; }
    }
}
