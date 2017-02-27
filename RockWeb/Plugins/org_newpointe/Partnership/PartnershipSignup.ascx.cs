﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Attribute;
using Rock.Communication;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using System.Web.UI.WebControls;
using System.Data;
using System.Web;
using DotLiquid.Tags;
using Quartz.Util;

namespace RockWeb.Plugins.org_newpointe.Partnership
{

    /// <summary>
    /// Block to pick a person and get their URL encoded key.
    /// </summary>
    [DisplayName( "Partnership Signup" )]
    [Category( "NewPointe Partnership" )]
    [Description("Partnership Signup")]

    [CodeEditorField("Partnership Text", "The text of the Partnership Agreement <span class='tip tip-lava'></span> <span class='tip tip-html'></span>", CodeEditorMode.Html, CodeEditorTheme.Rock, 400, true)]
    [BooleanField("Send Confirmation Email","Should we send a confirmation email?",true)]
    [CodeEditorField("Email Body", "The body text of the email that gets sent on completion <span class='tip tip-lava'></span> <span class='tip tip-html'></span>", CodeEditorMode.Html, CodeEditorTheme.Rock, 400, true)]
    [IntegerField("Age Requirement","Person must be at least this old to sign up.",true,16)]



    public partial class PartnershipSignup : Rock.Web.UI.RockBlock
    {

        RockContext rockContext = new RockContext();
        Person _targetPerson = new Person();
        public DateTime CurrentDateTime = DateTime.Now;
        public int CurrentYearAdd = 0;
        public string RequiredAge = "16";


        protected override void OnLoad( EventArgs e )
        {
            if ( !IsPostBack )
            {

                var campusCacheEnabled = CampusCache.All().AsQueryable().Where(c => c.IsActive == true).ToList();
                cpCampus.DataSource = campusCacheEnabled;
                cpCampus.DataBind();

                // Resolve the text field merge fields
                var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields(this.RockPage, _targetPerson);
                if (_targetPerson != null)
                {
                    mergeFields.Add("Person", _targetPerson);
                }

                lPartnershipText.Text = GetAttributeValue("PartnershipText").ResolveMergeFields(mergeFields);

            }

            int ageRequirement = Convert.ToInt32(GetAttributeValue("AgeRequirement"));
            RequiredAge = GetAttributeValue("AgeRequirement");


            if (DateTime.Now.Month == 12)
            {
                CurrentYearAdd++;
                CurrentDateTime = CurrentDateTime.AddYears(CurrentYearAdd);
            }

            lYear.Text = CurrentDateTime.Year.ToString();


            if (CurrentPerson != null)
            {
                _targetPerson = CurrentPerson;
                var currentPersonCampus = CurrentPerson.GetCampus();

                if (currentPersonCampus == null)
                {
                    mdCampus.Show();
                    tbSignature.Required = false;
                }

                if (_targetPerson.Age != null)
                {
                    if (_targetPerson.Age < ageRequirement)
                    {
                        mdNotLoggedIn.Show(String.Format("Sorry, you must be at least {0} to sign the NewPointe Partnership Covenant", RequiredAge),ModalAlertType.Alert);

                        pnlSignup.Visible = false;
                        pnlNotSixteen.Visible = true;
                        pnlSignature.Visible = false;
                    }

                }
                else
                {
                    mdAge.Show();
                    tbSignature.Required = false;
                }

                lPersonInfo.Text = _targetPerson.FullName;

            }
            else
            {
                string path = HttpContext.Current.Request.Url.AbsolutePath;
                path = path.UrlEncode();

                mdNotLoggedIn.Show( string.Format("Before you can sign the Partnership Covenant, you must log in with your MyNewPointe account.<br /><br /> <p class=\"text-center\"><a href = \"https://newpointe.org/Login?returnurl={0} \" class=\"btn btn-newpointe\">LOG IN</a> <a href = \"https://newpointe.org/NewAccount?returnurl={0} \" class=\"btn btn-newpointe\">REGISTER</a></p>", path), ModalAlertType.Alert);

                pnlSignup.Visible = false;
                pnlNotLoggedIn.Visible = true;
                pnlSignature.Visible = false;
            }


        }

        protected string GetDiscoverAttendanceInfo(Person thePerson)
        {
            string lastAttendedDate = "never";

            AttendanceService attendanceService = new AttendanceService(rockContext);
            GroupService groupService = new GroupService(rockContext);

            var discoverGroups = groupService.Queryable().Where(g => g.Name.Contains("DISCOVER My Church"));

            var discoverGroupAttendance = attendanceService.Queryable().Where(a => discoverGroups.Contains(a.Group) && a.PersonAliasId == thePerson.PrimaryAliasId);

            if (discoverGroupAttendance.Any())
            {
                discoverGroupAttendance = discoverGroupAttendance.OrderByDescending(a => a.StartDateTime);
                lastAttendedDate =
                    discoverGroupAttendance.Select(a => a.StartDateTime).FirstOrDefault().ToShortDateString();
            }

            return lastAttendedDate;
        }


        protected string UpcomingDiscover(Campus theCampus, string theTitle)
        {

            string upcomingDiscovers = "";

            ContentChannelItemService contentChannelItemService = new ContentChannelItemService(rockContext);

            List<ContentChannelItem> contentChannelItemsList = new List<ContentChannelItem>();

            var upcoming =
                contentChannelItemService.Queryable().Where(a => a.ContentChannelId == 14 && a.Title.Contains(theTitle) && a.StartDateTime >= DateTime.Now);


            foreach (var x in upcoming)
            {
                x.LoadAttributes();

                var campus = x.AttributeValues["Campus"];

                if (campus.ValueFormatted == theCampus.Name)
                    {
                    contentChannelItemsList.Add(x);
                    }

            }

            foreach (var x in contentChannelItemsList)
            {
                x.LoadAttributes();

                string registrationLink = "";

                if (x.AttributeValues["RegistrationLink"].ValueFormatted != "")
                {
                    registrationLink = String.Format("<a href= \"{0}\">Register Now!</a>",
                        x.AttributeValues["RegistrationLink"].Value);
                }

                upcomingDiscovers += String.Format("Date: {0} at {1}. Location: {2}. {3} <br>", x.StartDateTime.ToShortDateString(),
                    x.StartDateTime.ToShortTimeString(), x.AttributeValues["Location"], registrationLink);
            }

            if (!contentChannelItemsList.Any())
            {
                upcomingDiscovers = String.Format("There are not upcoming {0} Opportinuties at the {1}.", theTitle,
                    theCampus.Name);
            }
     

            return upcomingDiscovers;
        }



        protected string GetServingInfo(Person thePerson)
        {

            string volunteerGroupsString = "<a href= \"https://newpointe.org/VolunteerOpportunities \">Click here</a> to check out some incredible serving opportunities at NewPointe. NewPointe Volunteers are making a difference all across Northeast Ohio!";
            string teamTerm = "team";

            GroupMemberService groupMemberService = new GroupMemberService(rockContext);

            var volunteerGroups = groupMemberService.Queryable().Where(m => m.PersonId == thePerson.Id && m.Group.GroupTypeId == 42).Select(m => m.Group.Name).ToList();

            string joined = string.Join(", ", volunteerGroups);


            if (volunteerGroups.Count > 0)
            {
                if (volunteerGroups.Count > 1)
                {
                    teamTerm = "teams";
                }

                volunteerGroupsString = String.Format("Thanks for volunteering on the {0} {1}! Your service is making a difference all across Northeast Ohio.", joined, teamTerm);
            }

            
            return volunteerGroupsString;
        }


        protected void btnSubmit_OnClick(object sender, EventArgs e)
        {
            pnlOpportunities.Visible = true;
            pnlSignature.Visible = false;
            pnlSuccess.Visible = true;

            AttributeValueService attributeValueService = new AttributeValueService(rockContext);
            PersonService personService = new PersonService(rockContext);

            List<Guid> personGuidList = new List<Guid>();
            personGuidList.Add(_targetPerson.Guid);

            var p = attributeValueService.GetByAttributeIdAndEntityId(906, _targetPerson.Id);
            var p2 = attributeValueService.GetByAttributeIdAndEntityId(1434, _targetPerson.Id);


            var personFromService = personService.GetByGuids(personGuidList).FirstOrDefault();

            DateTime dateToSave = DateTime.Now.AddYears(CurrentYearAdd);

            p.Value = dateToSave.ToString();

            if (p2.Value.IsNullOrWhiteSpace())
            {
                p2.Value = dateToSave.Year.ToString();
            }
            else
            {
                if (!p2.Value.Contains(dateToSave.Year.ToString()))
                {
                    p2.Value = p2.Value + "," + dateToSave.Year.ToString();
                }
                
            }

            
            personFromService.ConnectionStatusValueId = 65;


            rockContext.SaveChanges();


            LoadOpportunities();

            if (GetAttributeValue("SendConfirmationEmail") == "True")
            {
                SendEmail(personFromService.Email, CurrentDateTime.Year.ToString() + " Partnership Covenant", rockContext);
            }

            


        }

        protected void mdCampus_OnSaveClick(object sender, EventArgs e)
        {
            GroupService groupService = new GroupService(rockContext);
            CampusService campusService = new CampusService(rockContext);

            var personFamily = _targetPerson.GetFamilies(rockContext).FirstOrDefault();

            var theGroup = groupService.Queryable().Where(a => a.Id == personFamily.Id).FirstOrDefault();

            var theCampus = campusService.Queryable().Where(c => c.Name == cpCampus.SelectedValue).FirstOrDefault();


            theGroup.Campus = theCampus;

            rockContext.SaveChanges();

            mdCampus.Hide();



        }

        protected void LoadOpportunities()
        {
            var currentPersonCampus = CurrentPerson.GetCampus();
            string lastAttended = GetDiscoverAttendanceInfo(_targetPerson);
            string volunteerGroups = GetServingInfo(_targetPerson);

            lDiscover.Text = String.Format("You attended DISCOVER My Church on {0}.", lastAttended);

            if (lastAttended == "never")
            {
                lDiscover.Text = "Upcoming DISCOVER My Church Opportunities at the " + currentPersonCampus.Name + ":<br />" + UpcomingDiscover(currentPersonCampus, "DISCOVER My Church");
            }

            lServing.Text = volunteerGroups;

            lGiving.Text = "Thanks for Giving! Your donations are making an eternal difference.  <a href= \"https://newpointe.org/GiveNow \">Click here</a> to start or manage your online giving at NewPointe - it's quick and easy!";

            lPersonInfo.Text = _targetPerson.FullName;
        }



        private void SendEmail(string recipient, string subject, RockContext rockContext)
        {

            // Resolve the text field merge fields
            var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields(this.RockPage, _targetPerson);
            if (_targetPerson != null)
            {
                mergeFields.Add("Person", _targetPerson);
            }

            string bodyText = GetAttributeValue("EmailBody").ResolveMergeFields(mergeFields);


            //Get CP Signature
            var campus = _targetPerson.GetCampus();
            var sig = "";
            campus.LoadAttributes();
            var signatureImage = campus.AttributeValues["CampusPastorSignature"].Value;
            if (!signatureImage.IsNullOrWhiteSpace())
            {
                sig =
                    String.Format(
                        "<img src='https://newpointe.org/GetImage.ashx?guid={0}&width=300' class='img-responsive' />",
                        signatureImage);
            }
           

            // Email Body
            string body = String.Format(@"{0}<br>
            <p>Sincerely,<br>
            <span style='font-size:125%; text-transform: uppercase; font-weight: bold;'>{1}<br>{2}</span><br>
            {3} Pastor
            ", bodyText, sig, campus.LeaderPersonAlias.Person.FullName, campus.Name);

            var fromEmailAddress = _targetPerson.GetCampus().LeaderPersonAlias.Person.Email;
            var fromEmailName = _targetPerson.GetCampus().LeaderPersonAlias.Person.FullName;
            var fromEmail = String.Format("{0}<{1}>", fromEmailName, fromEmailAddress);

            // Get the Header and Footer
            string emailHeader = Rock.Web.Cache.GlobalAttributesCache.Value("EmailHeader");
            string emailFooter = Rock.Web.Cache.GlobalAttributesCache.Value("EmailFooter");

            var recipients = new List<string>();
            recipients.Add(recipient);

            var mediumData = new Dictionary<string, string>();
            mediumData.Add("From", fromEmail);
            mediumData.Add("Subject", subject);
            mediumData.Add("Body", emailHeader + body + emailFooter);

            var mediumEntity = EntityTypeCache.Read(Rock.SystemGuid.EntityType.COMMUNICATION_MEDIUM_EMAIL.AsGuid(), rockContext);
            if (mediumEntity != null)
            {
                var medium = MediumContainer.GetComponent(mediumEntity.Name);
                if (medium != null && medium.IsActive)
                {
                    var transport = medium.Transport;
                    if (transport != null && transport.IsActive)
                    {
                        var appRoot = GlobalAttributesCache.Read(rockContext).GetValue("InternalApplicationRoot");
                        transport.Send(mediumData, recipients, appRoot, string.Empty);
                    }
                }
            }
        }


        protected void mdAge_OnSaveClick(object sender, EventArgs e)
        {
            PersonService personService = new PersonService(rockContext);

            List<Guid> personGuidList = new List<Guid>();
            personGuidList.Add(_targetPerson.Guid);
            var personFromService = personService.GetByGuids(personGuidList).FirstOrDefault();

            personFromService.SetBirthDate(dpBirthDate.SelectedDate);

            rockContext.SaveChanges();

            mdAge.Hide();

            Response.Redirect(Request.RawUrl);

        }
    }
}