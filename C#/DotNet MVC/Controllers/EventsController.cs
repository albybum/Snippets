using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Orientation.Models;
using Orientation.Models.ViewModels;
using Orientation.Classes;

namespace Orientation.Controllers
{
    
    [Authorize(Order = 3)]
    [AuthorizeRoles(Roles = "Staff,Admin,OIT", Order = 2)]
    [ActiveSessionUser(Order = 1)]
    public class EventsController : ControllerBase
    {
        private OrientationDB _ODB = new OrientationDB();

        //
        // GET: /Events/

        public ActionResult Index()
        {
            EventViewModel Model = new EventViewModel();
            return View(Model);
        }

        /// <summary>
        /// Displays Event Details
        /// </summary>
        /// <param name="ID">Event ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult Event(int ID) {
            tblEvent Event = _ODB.tblEvents.Where(e => e.ID == ID).SingleOrDefault();
            EventViewModel Model = new EventViewModel();
            if (Event != null) {
                Model = new EventViewModel(Event);
            }
            else
                this.ShowPageError(string.Format("An event could not be found for the given ID {0}",ID));

            return View(Model);
        }

        /// <summary>
        /// Displays Event Date Details
        /// </summary>
        /// <param name="ID">Event Date ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult EventDate(int ID) {
            tblEventDate EventDate = _ODB.tblEventDates.Where(e => e.ID == ID).SingleOrDefault();
            EventDateViewModel Model = new EventDateViewModel();
            if (EventDate != null) {
                Model = new EventDateViewModel(EventDate);
            }
            else
                this.ShowPageError(string.Format("An event date could not be found for the given ID {0}", ID));

            return View(Model);
        }


        /// <summary>
        /// Ajax: List of Events
        /// </summary>
        /// <param name="filterTerm">Term code</param>
        /// <param name="query">Query String</param>
        /// <returns>ActionResult</returns>
        [HttpAjaxRequest]
        public ActionResult List(string filterTerm = null, string query = null) {
            List<tblEvent> Model = new List<tblEvent>();
            IQueryable<tblEvent> FilteredResults = _ODB.tblEvents.AsQueryable();

            if (!string.IsNullOrEmpty(filterTerm))
                FilteredResults = FilteredResults.Where(e => e.Term == filterTerm).AsQueryable();

            if (!string.IsNullOrEmpty(query)) {
                FilteredResults = FilteredResults.Where(e =>
                    e.Name.ToUpper().Contains(query.ToUpper()) ||
                    e.Description.ToUpper().Contains(query.ToUpper()) ||
                    e.Term.ToUpper().Contains(query.ToUpper())
                ).AsQueryable();
            }

            Model = FilteredResults.ToList();
            return View("EventsList", Model);
        }

        /// <summary>
        /// Ajax: Lists Event Dates
        /// </summary>
        /// <param name="ID">Event ID</param>
        /// <returns>ActionResult</returns>
        [HttpAjaxRequest]
        public ActionResult DatesList(int ID) {
            List<tblEventDate> Model = new List<tblEventDate>();
            IQueryable<tblEventDate> FilteredResults = _ODB.tblEventDates.Where(e=>e.EventID == ID).AsQueryable();

            Model = FilteredResults.ToList();
            return View("EventDatesList", Model);
        }

        /// <summary>
        /// List of Invitations for the given Event
        /// </summary>
        /// <param name="ID">Event ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult Invitations(int ID) {
            tblEvent Event = _ODB.tblEvents.Where(e => e.ID == ID).SingleOrDefault();
            List<tblInvitation> Model = new List<tblInvitation>();
            if (Event != null) {
                Model = Event.tblInvitations.ToList();
                ViewBag.Event = Event;
            }
            else
                this.ShowPageError(String.Format("An Event could not be found for the given ID: {0}", ID));

            return View(Model);
        }

        // Mutators

        /// <summary>
        /// Creates an Event
        /// </summary>
        /// <param name="Model">Event View Model</param>
        /// <returns>ActionResult</returns>
        [HttpPost]
        public ActionResult Create(EventViewModel Model) {
            tblEvent Event = new tblEvent();
            if (ModelState.IsValid) {
                Event = Model.As_tblEvent();
                _ODB.tblEvents.AddObject(Event);
                _ODB.SaveChanges();
                this.ShowPageMessage(String.Format("The event '{0}' was created successfully.",Event.Name));
            }
            else {
                this.ShowPageError(DataConstants.ModelStateInvalidError);
                return View("Index",Model);
            }

            SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit, String.Format("User {0} created an event with ID {1}; Name: '{2}.'",Current.User.Username,Event.ID, Event.Name));
            return this.RedirectToAction<EventsController>(c => c.Event(Event.ID));
        }

        /// <summary>
        /// Creates an Event Date
        /// </summary>
        /// <param name="Model">Event Date View Model</param>
        /// <returns>ActionResult</returns>
        [HttpPost]
        public ActionResult CreateEventDate(EventDateViewModel Model) {
            tblEventDate EventDate = new tblEventDate();
            if (ModelState.IsValid) {
                EventDate = Model.As_tblEventDate();
                _ODB.tblEventDates.AddObject(EventDate);
                _ODB.SaveChanges();
                this.ShowPageMessage(String.Format("The event date for '{0}' was created successfully.", EventDate.DateOfEvent.ToShortDateString()));
            }
            else {
                this.ShowPageError(DataConstants.ModelStateInvalidError);
                tblEvent existingEvent = _ODB.tblEvents.Where(e => e.ID == Model.EventID).SingleOrDefault();
                if (existingEvent != null) {
                    return View("Event", new EventViewModel(existingEvent));
                }
                else {
                    this.ShowPageError(DataConstants.ModelStateInvalidError + " Also, an event could not be found for the given Event ID.");
                    return this.RedirectToAction<EventsController>(c => c.Index());
                }
            }

            SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit, String.Format("User {0} created an event date with ID {1}; Date: '{2}.'", Current.User.Username, EventDate.ID,EventDate.DateOfEvent.ToShortDateString()));
            return this.RedirectToAction<EventsController>(c => c.Event(EventDate.EventID));
        }

        /// <summary>
        /// Saves changes to an Event
        /// </summary>
        /// <param name="Model">Event View Model</param>
        /// <returns>ActionResult</returns>
        [HttpPost]
        public ActionResult Save(EventViewModel Model) {
            tblEvent existingEvent = _ODB.tblEvents.Where(e => e.ID == Model.ID).SingleOrDefault();
            if (existingEvent != null) {
                if (ModelState.IsValid) {                    
                    existingEvent.EventTypeExclusion = Model.EventTypeExclusion;
                    existingEvent.Description = Model.Description;
                    existingEvent.EventTypeID = Model.EventTypeID;
                    existingEvent.Fee = Model.Fee;
                    existingEvent.ID = Model.ID;
                    existingEvent.InvitationEmailID = Model.InvitationEmailID;
                    existingEvent.IsEnabled = Model.IsEnabled;
                    existingEvent.LaunchID = Model.LaunchID;
                    existingEvent.Name = Model.Name;
                    existingEvent.QuizID = Model.QuizID;
                    existingEvent.Term = Model.Term;
                    existingEvent.Blacklist = Model.Blacklist;
                    existingEvent.Whitelist = Model.Whitelist;
                    existingEvent.MarketplaceSite = Model.MarketplaceSite;
                    _ODB.SaveChanges();
                    this.ShowPageMessage(String.Format("The event '{0}' was updated successfully.", existingEvent.Name));
                    SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit,String.Format("User {0} updated Event {1}.",Current.User.Username,existingEvent.ID));
                    return this.RedirectToAction<EventsController>(c => c.Event(existingEvent.ID));
                }
                else {
                    this.ShowPageError(DataConstants.ModelStateInvalidError);
                    return View("Event", Model);
                }
            }
            else {
                this.ShowPageError("An event could not be found for the given Event ID.");
                return this.RedirectToAction<EventsController>(c => c.Index());
            }
        }

        /// <summary>
        /// Saves changes to an Event Date
        /// </summary>
        /// <param name="Model">Event Date View Model</param>
        /// <returns>ActionResult</returns>
        [HttpPost]
        public ActionResult SaveEventDate(EventDateViewModel Model) {
            tblEventDate existingEventDate = _ODB.tblEventDates.Where(e => e.ID == Model.ID).SingleOrDefault();
            if (existingEventDate != null) {
                if (ModelState.IsValid) {
                    existingEventDate.ConfirmationEmailID = Model.ConfirmationEmailID;
                    existingEventDate.ReminderEmailID = Model.ReminderEmailID;
                    existingEventDate.DateOfEvent = Model.DateOfEvent;
                    existingEventDate.EventDateType = Model.EventDateType;
                    existingEventDate.EventID = Model.EventID;
                    existingEventDate.ID = Model.ID;
                    existingEventDate.IsEnabled = Model.IsEnabled;
                    existingEventDate.MaxNumberOfStudents = Model.MaxNumberOfStudents;
                    existingEventDate.AutomaticallyClearSOHolds = Model.AutomaticallyClearSOHolds;
                    existingEventDate.Description = Model.Description;
                    existingEventDate.AllowGuestRegistration = Model.AllowGuestRegistration;
                    existingEventDate.MaxNumberGuests = Model.MaxNumberGuests;
                    existingEventDate.CostPerGuest = Model.CostPerGuest;
                    existingEventDate.UpdateSGBSTDN_ORSN = Model.UpdateORSN;
                    existingEventDate.HideEventDate = Model.HideEventDate;
                    _ODB.SaveChanges();
                    this.ShowPageMessage(String.Format("Event Date '{0}' was updated successfully.", existingEventDate.DateOfEvent.ToShortDateString()));
                    SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit,String.Format("User {0} updated Event Date {1}.",Current.User.Username,existingEventDate.ID));
                    return this.RedirectToAction<EventsController>(c => c.Event(existingEventDate.EventID));
                }
                else {
                    tblEvent existingEvent = _ODB.tblEvents.Where(e => e.ID == Model.EventID).SingleOrDefault();
                    if (existingEvent != null) {
                        return View("Event", new EventViewModel(existingEvent));
                    }
                    else {
                        this.ShowPageError(DataConstants.ModelStateInvalidError + " Also, an event could not be found for the given Event ID.");
                        return this.RedirectToAction<EventsController>(c => c.Index());
                    }
                }
            }
            else {
                this.ShowPageError("An event could not be found for the given Event ID.");
                return this.RedirectToAction<EventsController>(c => c.Index());
            }
        }

        /// <summary>
        /// Deletes Selected Event
        /// </summary>
        /// <param name="ID">Event ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult Delete(int ID) {
            tblEvent existingEvent = _ODB.tblEvents.Where(e => e.ID == ID).SingleOrDefault();
            if (existingEvent != null) {
                if (existingEvent.tblInvitations.Count > 0) 
                    this.ShowPageError(string.Format("Event '{0}' could not be deleted because it has existing student invitations.", existingEvent.Name));
                else if (existingEvent.tblEventDates.Count > 0) 
                    this.ShowPageError(string.Format("Event '{0}' could not be deleted because it has existing event dates.", existingEvent.Name));
                else {
                    string eventName = existingEvent.Name;
                    _ODB.DeleteObject(existingEvent);
                    _ODB.SaveChanges();
                    this.ShowPageMessage(string.Format("Event '{0}' was successfully deleted.",eventName));
                    SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit, String.Format("User {0} deleted event with ID {1}.",Current.User.Username,ID));
                }
                
            }
            else
                this.ShowPageError(string.Format("An event could not be found for the given ID. Nothing was deleted.", ID));

            return this.RedirectToAction<EventsController>(c => c.Index());
        }

        /// <summary>
        /// Deletes selected Event Date
        /// </summary>
        /// <param name="ID">Event Date ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult DeleteEventDate(int ID) {
            tblEventDate existingEventDate = _ODB.tblEventDates.Where(e => e.ID == ID).SingleOrDefault();
            if (existingEventDate != null) {
                if (existingEventDate.tblReservations.Count > 0)
                    this.ShowPageError(string.Format("Event Date '{0}' could not be deleted because it has existing student reservations.", existingEventDate.DateOfEvent.ToShortDateString()));
                else {
                    string eventDate = existingEventDate.DateOfEvent.ToShortDateString();
                    _ODB.DeleteObject(existingEventDate);
                    _ODB.SaveChanges();
                    this.ShowPageMessage(string.Format("Event Date '{0}' was successfully deleted.", eventDate));
                    SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit, String.Format("User {0} deleted event date with ID {1}.", Current.User.Username, ID));
                }

            }
            else {
                this.ShowPageError(string.Format("An event date could not be found for the given ID. Nothing was deleted.", ID));
                return this.RedirectToAction<EventsController>(c => c.Index());
            }

            return this.RedirectToAction<EventsController>(c => c.Event(existingEventDate.EventID));
        }


        /// <summary>
        /// Ajax: Changes event status between enabled and disabled. Acts as a toggle
        /// </summary>
        /// <param name="ID">Event ID</param>
        /// <returns>status message</returns>
        [HttpAjaxRequest]
        public string ToggleEventStatus(int ID) {
            string retVal = string.Empty;
            tblEvent Event = _ODB.tblEvents.Where(e => e.ID == ID).SingleOrDefault();
            if (Event != null) {
                Event.IsEnabled = !Event.IsEnabled;
                _ODB.SaveChanges();
                retVal = String.Format("Event '{0}' was successfully {1}", Event.Name, Event.IsEnabled ? "Enabled" : "Disabled");
                SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit, String.Format("User {0} updated status for event with ID {1}.", Current.User.Username, ID));
            }
            else
                retVal = String.Format("Could not find an event for the given ID {0}",ID);

            return retVal;
        }

        /// <summary>
        /// Ajax: Changes event date status between enabled and disabled. Acts as a toggle.
        /// </summary>
        /// <param name="ID">Event Date ID</param>
        /// <returns>status message</returns>
        [HttpAjaxRequest]
        public string ToggleEventDateStatus(int ID) {
            string retVal = string.Empty;
            tblEventDate EventDate = _ODB.tblEventDates.Where(e => e.ID == ID).SingleOrDefault();
            if (EventDate != null) {
                EventDate.IsEnabled = !EventDate.IsEnabled;
                _ODB.SaveChanges();
                retVal = String.Format("Event Date '{0}' was successfully {1}", EventDate.DateOfEvent.ToShortDateString(), EventDate.IsEnabled ? "Enabled" : "Disabled");
                SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit, String.Format("User {0} updated status for event date with ID {1}.", Current.User.Username, ID));
            }
            else
                retVal = String.Format("Could not find an event date for the given ID {0}", ID);

            return retVal;
        }


        public ActionResult ResendInvitationEmail(int ID) {
            tblEvent Event = _ODB.tblEvents.Where(e => e.ID == ID).SingleOrDefault();
            if (Event != null) {
                SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit, string.Format("User {0} is resending invitation emails for Event '{1}'",Current.User.Username,Event.Name));
                Classes.Notifications.EventInvitation(Event, Event.tblInvitations.ToList());
                this.ShowPageMessage(string.Format("Notifications have been processed for Event: '{0}'. The '{1}' email account will be notified once done.",Event.Name,Settings.DefaultEmailTo));
            }
            else
                this.ShowPageError(string.Format("An event could not be found for the given ID {0}", ID));

            return this.RedirectToAction<EventsController>(c => c.Event(ID));
        }



        /// <summary>
        /// Sends Event Date Reminder for the selected Event Date
        /// </summary>
        /// <param name="ID">Event Date ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult EventDateReminder(int ID) {
            tblEventDate EventDate = _ODB.tblEventDates.Where(e => e.ID == ID).SingleOrDefault();
            EventDateViewModel Model = new EventDateViewModel();
            if (EventDate != null) {
                Notifications.EventDateReminder(EventDate);
                this.ShowPageMessage(string.Format("Sending reminder for Event Date: '{0}' taking place on {1} to confirmed reservations.", EventDate.Description, EventDate.DateOfEvent.ToShortDateString()));
                return this.RedirectToAction<EventsController>(c => c.Event(EventDate.EventID));
            }
            else
                this.ShowPageError(string.Format("An event date could not be found for the given ID {0}", ID));

            return this.RedirectToAction<EventsController>(c => c.Index());
        }

    }
}
