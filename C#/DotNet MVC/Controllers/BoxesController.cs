using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text.RegularExpressions;
using POBox.Models;
using POBox.Models.ViewModels;
using POBox.Classes;
using SecureAccess.Utilities;

namespace POBox.Controllers
{

    [AuthorizeRoles(Roles = "POEmployee,Admin", Order = 2)]
    [ActiveSessionUser(Order = 1)]
    public class BoxesController : ControllerBase
    {

        private POBox.Models.POBoxContainer _POBoxDB = new Models.POBoxContainer();

        //
        // GET: /Boxes/

        public ActionResult Index()
        {

            List<SelectListItem> BoxLocations = new List<SelectListItem>() { 
                new SelectListItem() { Text = "No Preference", Value = "NoPreference" }, 
                new SelectListItem() { Text = "Top", Value = "Top" },
                new SelectListItem() { Text = "Bottom", Value = "Bottom" } 
            };
            ViewBag.BoxLocationOptions = BoxLocations;
            ViewBag.EligibleAssignmentBoxesWithForwards = _POBoxDB.AssignmentEligibileBoxesWithForwards;
            return View();
        }

        public ActionResult Modify(int? ID) {
            BoxDetailsViewModel Model = new BoxDetailsViewModel();

            if (ID.HasValue && ID.Value > 0) {

                tblPOBox box = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == ID.Value).SingleOrDefault();

                if (box != null)
                    Model = new BoxDetailsViewModel(box);
                else
                    this.ShowPageError(String.Format("The requested box could not be found: {0}", ID.Value));
            }
            else {
                return this.RedirectToAction<BoxesController>(c => c.Index());
            }

            return View(Model);
        }
         
        public ActionResult MailForward(int ID) {
            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == ID).SingleOrDefault();
            BoxDetailsViewModel Model = new BoxDetailsViewModel();

            if (assignment != null && assignment.tblPOBox != null) {
                Model = new BoxDetailsViewModel(assignment.tblPOBox);
                Model.CurrentAssignment = new Assignment(assignment); // override current assignment to the one we're looking at for the view model
            }
            else
                this.ShowPageError(String.Format("The requested box could not be found: {0}", ID));

            return View(Model);
        }

        public ActionResult AlternateAddress(int ID) {
            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == ID).SingleOrDefault();
            BoxDetailsViewModel Model = new BoxDetailsViewModel();

            if (assignment != null && assignment.tblPOBox != null) {
                Model = new BoxDetailsViewModel(assignment.tblPOBox);
                Model.CurrentAssignment = new Assignment(assignment); // override current assignment to the one we're looking at for the view model
            }
            else
                this.ShowPageError(String.Format("The requested box could not be found: {0}", ID));

            return View(Model);
        }

        [HttpPost]
        public ActionResult QuickAssignStudent(string ENumberOrUsername, string BoxLocation) {
            Regex regENumber = new Regex(@"^[Ee][0-9]{8}$");    // Reg-Ex for E########


            if(!String.IsNullOrEmpty(ENumberOrUsername)) {
                SecureAccess.Utilities.Banner.User user = null;
                if (regENumber.IsMatch(ENumberOrUsername))
                    user = ENumberOrUsername.GetUserByENumber();
                else
                    user = ENumberOrUsername.GetUserByADUsername(); 

                if (user != null) {
                    // look for active student assignments for the user's ENumber.  If exists: warn, otherwise: assign.
                    if (_POBoxDB.tblStudentAssignmentDetails.Where(e => e.ENumber.ToUpper() == user.ENumber.ToUpper() && e.tblAssignment.IsActive).Count() > 0) {
                        this.ShowPageError(String.Format("A student box assignment already exists for ENumber {0}",user.ENumber));
                    }
                    else {
                        string errors = DBHelper.AssignStudentBox(user.ENumber, this.Request.RequestContext, BoxLocation);
                        if (string.IsNullOrEmpty(errors)) {
                            tblStudentAssignmentDetail boxDetails = _POBoxDB.tblStudentAssignmentDetails.Where(e => e.ENumber.ToUpper() == user.ENumber.ToUpper() && e.tblAssignment.IsActive).FirstOrDefault();
                            this.ShowPageMessage(string.Format("Student {0} has been assigned to PO Box {1}.", user.ENumber, boxDetails.tblAssignment.BoxNumber));
                        }
                        else
                            this.ShowPageError(errors);
                    }
                }
                else {
                    this.ShowPageError(String.Format("Could not retrieve user by the specified ENumber or Username: {0}",ENumberOrUsername));
                }

            }
            else 
                this.ShowPageError("A username or ENumber must be provided");

            return this.RedirectToAction<BoxesController>(c=>c.Index());
        }


        [HttpPost]
        public ActionResult Save(BoxDetailsViewModel Model) {
            tblPOBox box = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == Model.BoxInfo.BoxNumber).SingleOrDefault();
            if (ModelState.IsValid) {

                bool notifyStudent = false;
                if (box != null) {

                    // check box type vs assignments
                    if (box.BoxTypeID != Model.BoxInfo.Type) {
                        // user is trying to change box type, make sure there are no active assignments
                        if (box.tblAssignments.Where(e => e.IsActive).Count() > 0) {
                            this.ShowPageError("You must remove any active assignments before changing a box type. Details not saved.");
                            BoxDetailsViewModel retEarly = new BoxDetailsViewModel(box);
                            return View("Modify", retEarly);
                        }
                    }

                    notifyStudent = (box.Combination != Model.BoxInfo.Combination);
                    box.Combination = Model.BoxInfo.Combination;
                    box.BoxTypeID = Model.BoxInfo.Type;
                    box.Location = Model.BoxInfo.Location;
                    box.IsAssignable = Model.BoxInfo.Assignable;
                    box.ReassignDate = Model.BoxInfo.ReassignDate;
                    _POBoxDB.SaveChanges();
                    if (notifyStudent)
                        Notifications.UpdatedStudentBox(box,this.Request.RequestContext);

                    Logging.Log(Logging.LogType.Audit, String.Format("User {0} updated box {1} details.",Current.User.Username,box.BoxNumber));
                    this.ShowPageMessage(String.Format("Box {0} was successfully updated.",box.BoxNumber));
                }
                else
                    this.ShowPageError(String.Format("Could not retrieve box for the given box number: {0}",Model.BoxInfo.BoxNumber));
            }
            else
                this.ShowPageError(DataConstants.ModelStateInvalidError);

            BoxDetailsViewModel returnModel = new BoxDetailsViewModel(box);
            return View("Modify", returnModel);
        }

        [HttpPost]
        public ActionResult Relocate(int CurrentBox, int NewBox) {
            if(CurrentBox <= 0) { this.ShowPageError("Current box number was not included when relocating an assignment."); return this.RedirectToAction<BoxesController>(c=>c.Index()); }

            tblPOBox currentBox = null;
            tblPOBox newBox = null;
            if (NewBox > 0) {
                currentBox = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == CurrentBox).SingleOrDefault();
                newBox = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == NewBox).SingleOrDefault();
                if (currentBox != null && newBox != null) {

                    // check box type
                    if (currentBox.BoxTypeID != newBox.BoxTypeID) {
                        this.ShowPageError("Both the current and new box must be the same type of box before any assignments can be moved.");
                        BoxDetailsViewModel returnModel = new BoxDetailsViewModel(currentBox);
                        return View("Modify", returnModel);
                    }

                    // check box assignability
                    if (!newBox.IsAssignable) {
                        this.ShowPageError("The new box selected is marked unassignable. Please select a different box.");
                        BoxDetailsViewModel returnModel = new BoxDetailsViewModel(currentBox);
                        return View("Modify", returnModel);
                    }

                    tblAssignment existingNewBoxAssignment = newBox.tblAssignments.Where(e => e.IsActive).SingleOrDefault();
                    if (existingNewBoxAssignment == null) {
                        tblAssignment activeAssignment = currentBox.tblAssignments.Where(e => e.IsActive).SingleOrDefault();

                        // deactivate existing assignment
                        activeAssignment.IsActive = false;
                        activeAssignment.UnassignedDate = DateTime.Now;
                        _POBoxDB.SaveChanges();

                        // create new assignment. Assign to new box based on box type.
                        if (currentBox.BoxTypeID == DataConstants.BoxTypeValues.Student) {
                            DBHelper.AssignStudentBox(activeAssignment.tblStudentAssignmentDetails.FirstOrDefault().ENumber, this.Request.RequestContext, null, NewBox);
                            // get new assignment and copy forwarding address, if exists
                            tblAssignment newAssignment = _POBoxDB.tblAssignments.Where(e => e.IsActive && e.BoxNumber == NewBox).SingleOrDefault();
                            if (activeAssignment.tblMailForwards.Count > 0) {
                                tblMailForward newForward = new tblMailForward();
                                tblMailForward oldForward = activeAssignment.tblMailForwards.SingleOrDefault();
                                newForward.AddressLine1 = oldForward.AddressLine1;
                                newForward.AddressLine2 = oldForward.AddressLine2;
                                newForward.AssignmentID = oldForward.AssignmentID;
                                newForward.City = oldForward.City;
                                newForward.EndDate = oldForward.EndDate;
                                newForward.IsTemporary = oldForward.IsTemporary;
                                newForward.StartDate = oldForward.StartDate;
                                newForward.State = oldForward.State;
                                newForward.ZIP = oldForward.ZIP;
                                newAssignment.tblMailForwards.Add(newForward);
                                // delete old forward
                                _POBoxDB.DeleteObject(oldForward);
                                _POBoxDB.SaveChanges();
                            }
                        }
                        else if (currentBox.BoxTypeID == DataConstants.BoxTypeValues.Rental) {
                            tblRentalAssignmentDetail rentalDetails = activeAssignment.tblRentalAssignmentDetails.SingleOrDefault();
                            bool success = _POBoxDB.AssignRentalBox(NewBox, rentalDetails.ContactName, rentalDetails.EmailAddress, rentalDetails.ContactPhone, rentalDetails.DueDate, rentalDetails.Fee);
                        }
                        else if (currentBox.BoxTypeID == DataConstants.BoxTypeValues.Departmental) {
                            tblDepartmentalAssignmentDetail deptDetails = activeAssignment.tblDepartmentalAssignmentDetails.SingleOrDefault();
                            bool success = _POBoxDB.AssignDepartmentalBox(NewBox, deptDetails.DepartmentName, deptDetails.ContactPhone, deptDetails.EmailAddress);
                            // get new assignment and copy alternate address, if exists
                            tblAssignment newAssignment = _POBoxDB.tblAssignments.Where(e => e.IsActive && e.BoxNumber == NewBox).SingleOrDefault();
                            if (activeAssignment.tblAlternateDeliveryLocations.Count > 0) {
                                tblAlternateDeliveryLocation newAddress = new tblAlternateDeliveryLocation();
                                tblAlternateDeliveryLocation oldAddress = activeAssignment.tblAlternateDeliveryLocations.SingleOrDefault();
                                newAddress.AddressLine1 = oldAddress.AddressLine1;
                                newAddress.AddressLine2 = oldAddress.AddressLine2;
                                newAddress.AssignmentID = oldAddress.AssignmentID;
                                newAddress.City = oldAddress.City;
                                newAddress.State = oldAddress.State;
                                newAddress.ZIP = oldAddress.ZIP;
                                newAddress.FirstName = oldAddress.FirstName;
                                newAddress.MiddleName = oldAddress.MiddleName;
                                newAddress.LastName = oldAddress.LastName;
                                newAssignment.tblAlternateDeliveryLocations.Add(newAddress);
                                // delete old forward
                                _POBoxDB.DeleteObject(oldAddress);
                                _POBoxDB.SaveChanges();
                            } 
                        }

                        Logging.Log(Logging.LogType.Audit, String.Format("User {2} moved an active box assignment from box {0} to box {1}.", CurrentBox, NewBox, Current.User.Username));
                        this.ShowPageMessage(String.Format("The active assignment was successfully moved from box {0} to box {1}.", CurrentBox, NewBox));
                        return this.RedirectToAction<BoxesController>(c => c.Modify(NewBox));
                    }
                    else {
                        this.ShowPageError("The new box specified already has an active assignment. Assignments can only be moved to boxes with no active assignments");
                        BoxDetailsViewModel returnModel = new BoxDetailsViewModel(currentBox);
                        return View("Modify", returnModel);
                    }
                }
                else {
                    this.ShowPageError("Either the current or new box could not be retrieved by the provided box numbers. The relocation was not completed. Please ensure you are providing a valid box number.");
                    BoxDetailsViewModel returnModel = new BoxDetailsViewModel(currentBox);
                    return View("Modify", returnModel);
                }
            }
            else {
                this.ShowPageError("A new box number must be specified to relocate active assignments.");
                return this.RedirectToAction<BoxesController>(c=>c.Modify(CurrentBox)); 
            }
        }

        [HttpPost]
        public ActionResult DeactivateAssignment(int AssignmentID) {
            bool success = DBHelper.UnassignBox(AssignmentID);
            if (success) {
                tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == AssignmentID).SingleOrDefault();
                this.ShowPageMessage("The selected assignment was successfully deactivated.");
                return this.RedirectToAction<BoxesController>(c => c.Modify(assignment.tblPOBox.BoxNumber));
            }
            else {
                this.ShowPageError("The specified assignment could not be found.");
                return this.RedirectToAction<BoxesController>(c => c.Index());
            }
        }


        public ActionResult EditAssignment(int AssignmentID) {
            BoxDetailsViewModel Model = new BoxDetailsViewModel();
            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == AssignmentID).SingleOrDefault();
            if (assignment != null) {
                Model = new BoxDetailsViewModel(assignment.tblPOBox);
                Logging.Log(Logging.LogType.Audit, string.Format("User {0} has chosen to edit assignment with ID {1}", Current.User.Username, AssignmentID), "EditAssignment", Logging.Severity.Information);
            }
            else {
                this.ShowPageError("An assignment could not be found for the given ID.");
            }
            return View(Model);
        }

        /// <summary>
        /// Converts student assignment to a rental assignment.  Used when a student gets caught in a non-student purge but wants to keep their box as an alumni.
        /// </summary>
        /// <param name="AssignmentID"></param>
        /// <returns></returns>
        public ActionResult ConvertAssignmentToRental(int AssignmentID) {
            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == AssignmentID).SingleOrDefault();
            if (assignment != null && assignment.tblStudentAssignmentDetails.FirstOrDefault() != null) {
                tblPOBox Box = assignment.tblPOBox;
                tblStudentAssignmentDetail studentDetails = assignment.tblStudentAssignmentDetails.FirstOrDefault();
                string Name = string.Format("{0}, {1} {2}", studentDetails.LastName, studentDetails.FirstName, studentDetails.MiddleName);
                string Email = studentDetails.Email;

                DBHelper.UnassignBox(assignment.ID); // unassign student assignment

                // change box type
                Box.BoxTypeID = DataConstants.BoxTypeValues.Rental;
                _POBoxDB.SaveChanges();

                // create rental assignment
                var throwaway = this.CreateRentalAssignment(Box.BoxNumber, Name, Email, string.Empty, DateTime.Now.AddYears(1), 0);

                _POBoxDB.LogEvent(DataConstants.EventLogCategories.ConvertStudentToRental, string.Format("User {0} has converted student assignment with ID {1} to a rental for Box {2}", Current.User.Username, AssignmentID, Box.BoxNumber), DateTime.Now, Box.BoxNumber);
                Logging.Log(Logging.LogType.Audit, string.Format("User {0} has converted student assignment with ID {1} to a rental for Box {2}", Current.User.Username, AssignmentID, Box.BoxNumber), "EditAssignment", Logging.Severity.Information);

                return this.RedirectToAction<BoxesController>(c => c.Modify(Box.BoxNumber));
            }
            else {
                this.ShowPageError("A student assignment could not be found for the given ID.");
                return this.RedirectToAction<BoxesController>(c=>c.Index());
            }
        }
        

        [HttpPost]
        public ActionResult SaveRentalAssignment(int ID, string ContactName, string Email, string ContactPhone, DateTime? DueDate, decimal? Fee) {
            if (ID <= 0) {
                this.ShowPageError("An assignment ID must be provided when saving a rental assignment.");
                return this.RedirectToAction<BoxesController>(c => c.Index());
            }

            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == ID).SingleOrDefault();

            if (assignment != null) {
                if (string.IsNullOrEmpty(ContactName) || !DueDate.HasValue || !Fee.HasValue) {
                    this.ShowPageError("First name, due date, and fee must be provided when saving a rental assignment.");
                }
                else {
                    if(assignment.AssignmentTypeID == DataConstants.AssignmentTypeValues.Rental) {
                        if(assignment.tblRentalAssignmentDetails.FirstOrDefault() != null) {
                            assignment.tblRentalAssignmentDetails.FirstOrDefault().ContactName = ContactName;
                            assignment.tblRentalAssignmentDetails.FirstOrDefault().EmailAddress = Email;
                            assignment.tblRentalAssignmentDetails.FirstOrDefault().ContactPhone = ContactPhone;
                            assignment.tblRentalAssignmentDetails.FirstOrDefault().DueDate = DueDate.Value;
                            assignment.tblRentalAssignmentDetails.FirstOrDefault().Fee = Fee.Value;
                            _POBoxDB.SaveChanges();
                            this.ShowPageMessage("Rental Assignment details have been saved successfully.");
                            Logging.Log(Logging.LogType.Audit, string.Format("User {0} updated rental assignment with ID {1}", Current.User.Username, ID), "EditAssignment", Logging.Severity.Information);
                        }
                        else 
                            this.ShowPageError("This assignment does not contain rental details");
                    }
                    else
                        this.ShowPageError("Error: Attempting to save rental details to an assignment that is not marked as a rental.");
                }
            }
            else {
                this.ShowPageError("An assignment could not be found for the given ID.");
            }

            return this.RedirectToAction<BoxesController>(c => c.EditAssignment(ID));
        }

        [HttpPost]
        public ActionResult SaveDepartmentAssignment(int ID, string DepartmentName, string ContactPhone, string EmailAddress) {
            if (ID <= 0) {
                this.ShowPageError("An assignment must be provided when saving a department assignment.");
                return this.RedirectToAction<BoxesController>(c => c.Index());
            }

            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == ID).SingleOrDefault();

            if (assignment != null) {
                if (string.IsNullOrEmpty(DepartmentName)) {
                    this.ShowPageError("A department name must be provided when saving a department assignment.");
                }
                else {
                    if (assignment.AssignmentTypeID == DataConstants.AssignmentTypeValues.Departmental) {
                        if (assignment.tblDepartmentalAssignmentDetails.FirstOrDefault() != null) {
                            assignment.tblDepartmentalAssignmentDetails.FirstOrDefault().DepartmentName = DepartmentName;
                            assignment.tblDepartmentalAssignmentDetails.FirstOrDefault().ContactPhone = ContactPhone;
                            assignment.tblDepartmentalAssignmentDetails.FirstOrDefault().EmailAddress = EmailAddress;
                            _POBoxDB.SaveChanges();
                            this.ShowPageMessage("Department Assignment details have been saved successfully.");
                            Logging.Log(Logging.LogType.Audit, string.Format("User {0} updated department assignment with ID {1}", Current.User.Username, ID), "EditAssignment", Logging.Severity.Information);
                        }
                        else
                            this.ShowPageError("This assignment does not contain department details");
                    }
                    else
                        this.ShowPageError("Error: Attempting to save department details to an assignment that is not marked as Departmental.");
                }
            }
            else {
                this.ShowPageError("An assignment could not be found for the given ID.");
            }

            return this.RedirectToAction<BoxesController>(c => c.EditAssignment(ID));
        }

        [HttpPost]
        public ActionResult SaveStudentAssignment(int ID) {
            if (ID <= 0) {
                this.ShowPageError("An assignment must be provided when saving a student assignment.");
                return this.RedirectToAction<BoxesController>(c => c.Index());
            }

            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == ID).SingleOrDefault();

            if (assignment != null) {
                    if (assignment.AssignmentTypeID == DataConstants.AssignmentTypeValues.Student) {
                        if (assignment.tblStudentAssignmentDetails.FirstOrDefault() != null) {
                            Banner.User BannerUser = assignment.tblStudentAssignmentDetails.FirstOrDefault().ENumber.GetUserByENumber();
                            if (BannerUser != null) {
                                BannerDB Banner = new BannerDB();
                                HousingDB Housing = new HousingDB();
                                if (Banner.Ping()) {
                                    if (Housing.Ping()) {
                                        assignment.tblStudentAssignmentDetails.FirstOrDefault().FirstName = BannerUser.FirstName;
                                        assignment.tblStudentAssignmentDetails.FirstOrDefault().LastName = BannerUser.LastName;
                                        assignment.tblStudentAssignmentDetails.FirstOrDefault().MiddleName = BannerUser.MiddleInitial;
                                        assignment.tblStudentAssignmentDetails.FirstOrDefault().Username = BannerUser.ADUsername;
                                        assignment.tblStudentAssignmentDetails.FirstOrDefault().Email = BannerUser.EmailAddress;
                                        assignment.tblStudentAssignmentDetails.FirstOrDefault().IsStudent = Banner.IsCurrentStudent(BannerUser.ENumber);
                                        assignment.tblStudentAssignmentDetails.FirstOrDefault().IsResident = Housing.IsCurrentResident(BannerUser.ENumber, BannerDB.GetCurrentTerm());
                                        _POBoxDB.SaveChanges();
                                        this.ShowPageMessage("Student Assignment details have been saved successfully.");
                                        Logging.Log(Logging.LogType.Audit, string.Format("User {0} updated student assignment with ID {1}",Current.User.Username,ID), "EditAssignment", Logging.Severity.Information);
                                    }
                                    else
                                        this.ShowPageError("Could not connect to Housing Database");
                                }
                                else
                                    this.ShowPageError("Could not connect to Banner");
                            }
                            else 
                                this.ShowPageError("Could not update student from Banner Cache");
                        }
                        else
                            this.ShowPageError("This assignment does not contain student details");
                    }
                    else
                        this.ShowPageError("Error: Attempting to save student details to an assignment that is not marked as Departmental.");
                
            }
            else {
                this.ShowPageError("An assignment could not be found for the given ID.");
            }

            return this.RedirectToAction<BoxesController>(c => c.EditAssignment(ID));
        }



        /// <summary>
        /// Saves the selected box number from being purged. 
        /// </summary>
        /// <param name="BoxNumber">Box Number</param>
        /// <returns>RedirectToRouteResult</returns>
        [HttpPost]
        public ActionResult RetainBox(int BoxNumber) {
            if (ModelState.IsValid) {
                Logging.Log(Logging.LogType.Audit, string.Format("User {0} is attempting to retain mailbox for box number {1}.", Current.User.Username,BoxNumber));

                // find the active assignment for the given box #
                tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.BoxNumber == BoxNumber && e.IsActive).SingleOrDefault();
                if (assignment != null) {
                    // keep assignment, reset date, save changes
                    assignment.PurgeType = DataConstants.PurgeStatusTypes.Keep;
                    assignment.MarkedForPurgeDate = null;
                    _POBoxDB.SaveChanges();
                    _POBoxDB.LogEvent(DataConstants.EventLogCategories.RetainedBox, string.Format("Box Number {0} was successfully retained by staff user {1}.", assignment.BoxNumber,Current.User.Username), DateTime.Now, assignment.BoxNumber);
                    Logging.Log(Logging.LogType.Audit, String.Format("Box Number {0} was successfully retained for assigned student.", assignment.BoxNumber));
                    this.ShowPageMessage(string.Format("Box assignment was retained. The following box will not be purged at this time: Box Number {0}", assignment.BoxNumber));
                    
                }
                else
                    this.ShowPageError(String.Format("An assignment could not be found for Box Number {0}", BoxNumber));
            }
            else
                this.ShowPageError(DataConstants.ModelStateInvalidError);

            return this.RedirectToAction<BoxesController>(c => c.Modify(BoxNumber));
        }

        [HttpPost]
        public ActionResult SaveForwardingAddress(BoxDetailsViewModel Model) {
            
            Logging.Log(Logging.LogType.Audit, string.Format("User {0} is attempting to save a forwarding address for assignment {1}.", Current.User.Username,Model.CurrentAssignment.ID));
            // get the assignment
            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == Model.CurrentAssignment.ID).SingleOrDefault();
            if (assignment != null) {
                if (ModelState.IsValid) {
                    if (Model.CurrentAssignment.MailForward.IsTemporaryForward) {
                        // check user-provided duration.
                        if (!Model.CurrentAssignment.MailForward.StartDate.HasValue || !Model.CurrentAssignment.MailForward.EndDate.HasValue) {
                            this.ShowPageError("An Temporary forward must specify a begin and end date");
                            BoxDetailsViewModel returnModel = new BoxDetailsViewModel(assignment.tblPOBox);
                            return View("Modify", returnModel);
                        }
                        if (Model.CurrentAssignment.MailForward.StartDate.Value.Date < DateTime.Now.Date) {
                            this.ShowPageError("The start date must be a future date.");
                            BoxDetailsViewModel returnModel = new BoxDetailsViewModel(assignment.tblPOBox);
                            return View("Modify", returnModel);
                        }
                        if (Model.CurrentAssignment.MailForward.EndDate.Value.Date <= DateTime.Now.Date) {
                            this.ShowPageError("The end date must be a future date.");
                            BoxDetailsViewModel returnModel = new BoxDetailsViewModel(assignment.tblPOBox);
                            return View("Modify", returnModel);
                        }
                        if (Model.CurrentAssignment.MailForward.EndDate.Value.Date <= Model.CurrentAssignment.MailForward.StartDate.Value.Date) {
                            this.ShowPageError("The end date must be after the start date.");
                            BoxDetailsViewModel returnModel = new BoxDetailsViewModel(assignment.tblPOBox);
                            return View("Modify", returnModel);
                        }
                        TimeSpan duration = Model.CurrentAssignment.MailForward.EndDate.Value.Subtract(Model.CurrentAssignment.MailForward.StartDate.Value);
                        if (duration.GetMonths() > 6) {
                            this.ShowPageError("An Temporary forward must not exceed 6 months.");
                            BoxDetailsViewModel returnModel = new BoxDetailsViewModel(assignment.tblPOBox);
                            return View("Modify", returnModel);
                        }

                    }
                    assignment.tblMailForwards.ToList().ForEach(e => _POBoxDB.DeleteObject(e));
                    assignment.tblMailForwards.Add(Model.CurrentAssignment.MailForward.As_tblMailForward());
                    _POBoxDB.SaveChanges();
                    _POBoxDB.LogEvent(DataConstants.EventLogCategories.AddMailForward, String.Format("A forwarding address was saved by user {1} for Box {0}.", assignment.BoxNumber, Current.User.Username), null,assignment.BoxNumber);
                    Logging.Log(Logging.LogType.Audit, String.Format("A forwarding address was saved by user {1}. Box {0}.", assignment.BoxNumber, Current.User.Username));
                    Notifications.ForwardingAdded(assignment.tblStudentAssignmentDetails.SingleOrDefault(), this.Request.RequestContext);
                    this.ShowPageMessage(String.Format("A forwarding address was saved by user {1}. Box {0}.", assignment.BoxNumber, Current.User.Username));

                    return this.RedirectToAction<BoxesController>(c => c.Modify(assignment.tblPOBox.BoxNumber));
                }
                else {
                    this.ShowPageError(DataConstants.ModelStateInvalidError);
                    BoxDetailsViewModel returnModel = new BoxDetailsViewModel(assignment.tblPOBox);
                    return View("Modify", returnModel);
                }
            }
            else
                this.ShowPageError(String.Format("An assignment could not be found for the given ID: {0}", Model.CurrentAssignment.ID));


            return this.RedirectToAction<BoxesController>(c => c.Index());
        }


        [HttpPost]
        public ActionResult DeleteForwardingAddress(BoxDetailsViewModel Model) {
            if (ModelState.IsValid) {
                Logging.Log(Logging.LogType.Audit, string.Format("User {0} is attempting to delete forwarding addresses for assignment {1}.", Current.User.Username,Model.CurrentAssignment.ID));
                // get the assignment
                tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == Model.CurrentAssignment.ID).SingleOrDefault();
                if (assignment != null) {
                    assignment.tblMailForwards.ToList().ForEach(e => _POBoxDB.DeleteObject(e));
                    _POBoxDB.SaveChanges();
                    _POBoxDB.LogEvent(DataConstants.EventLogCategories.RemoveMailForward, String.Format("All forwarding addresses were deleted by user {1} for Box {0}.", assignment.BoxNumber, Current.User.Username), null,assignment.BoxNumber);
                    Logging.Log(Logging.LogType.Audit, String.Format("All forwarding addresses were deleted by user {1}. Box {0}.", assignment.BoxNumber, Current.User.Username));
                    this.ShowPageMessage(string.Format("Forwarding address information was successfully removed. Box Number {0}", assignment.BoxNumber));

                    return this.RedirectToAction<BoxesController>(c => c.Modify(assignment.tblPOBox.BoxNumber));
                }
                else
                    this.ShowPageError(String.Format("An assignment could not be found for the given ID: {0}", Model.CurrentAssignment.ID));
            }
            else
                this.ShowPageError(DataConstants.ModelStateInvalidError);

            return this.RedirectToAction<BoxesController>(c => c.Index());
        }


        [HttpPost]
        public ActionResult SaveAlternateAddress(BoxDetailsViewModel Model) {

            Logging.Log(Logging.LogType.Audit, string.Format("User {0} is attempting to save an alternate address for assignment {1}.", Current.User.Username, Model.CurrentAssignment.ID));
            // get the assignment
            tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == Model.CurrentAssignment.ID).SingleOrDefault();
            if (assignment != null) {
                if (ModelState.IsValid) {
                    assignment.tblAlternateDeliveryLocations.ToList().ForEach(e => _POBoxDB.DeleteObject(e));
                    assignment.tblAlternateDeliveryLocations.Add(Model.CurrentAssignment.AlternateAddress.As_tblAlternateDeliveryLocation());
                    _POBoxDB.SaveChanges();
                    Logging.Log(Logging.LogType.Audit, String.Format("An alternate address was saved by user {1}. Box {0}.", assignment.BoxNumber, Current.User.Username));
                    this.ShowPageMessage(String.Format("An alternate address was saved by user {1}. Box {0}.", assignment.BoxNumber, Current.User.Username));

                    return this.RedirectToAction<BoxesController>(c => c.Modify(assignment.tblPOBox.BoxNumber));
                }
                else {
                    this.ShowPageError(DataConstants.ModelStateInvalidError);
                    BoxDetailsViewModel returnModel = new BoxDetailsViewModel(assignment.tblPOBox);
                    return View("Modify", returnModel);
                }
            }
            else
                this.ShowPageError(String.Format("An assignment could not be found for the given ID: {0}", Model.CurrentAssignment.ID));


            return this.RedirectToAction<BoxesController>(c => c.Index());
        }



        [HttpPost]
        public ActionResult DeleteAlternateAddress(BoxDetailsViewModel Model) {
            if (ModelState.IsValid) {
                Logging.Log(Logging.LogType.Audit, string.Format("User {0} is attempting to delete an alternate addresses for assignment {1}.", Current.User.Username, Model.CurrentAssignment.ID));
                // get the assignment
                tblAssignment assignment = _POBoxDB.tblAssignments.Where(e => e.ID == Model.CurrentAssignment.ID).SingleOrDefault();
                if (assignment != null) {
                    assignment.tblAlternateDeliveryLocations.ToList().ForEach(e => _POBoxDB.DeleteObject(e));
                    _POBoxDB.SaveChanges();
                    Logging.Log(Logging.LogType.Audit, String.Format("All alternate addresses were deleted by user {1}. Box {0}.", assignment.BoxNumber, Current.User.Username));
                    this.ShowPageMessage(string.Format("Alternate address information was successfully removed. Box Number {0}", assignment.BoxNumber));

                    return this.RedirectToAction<BoxesController>(c => c.Modify(assignment.tblPOBox.BoxNumber));
                }
                else
                    this.ShowPageError(String.Format("An assignment could not be found for the given ID: {0}", Model.CurrentAssignment.ID));
            }
            else
                this.ShowPageError(DataConstants.ModelStateInvalidError);

            return this.RedirectToAction<BoxesController>(c => c.Index());
        }

        [HttpPost]
        public ActionResult CreateStudentAssignment(string ENumberOrUsername, int BoxNumber) {
            if (BoxNumber <= 0) {
                this.ShowPageError("A box number must be provided when creating a student assignment.");
                return this.RedirectToAction<BoxesController>(c => c.Index());
            }
            if (string.IsNullOrEmpty(ENumberOrUsername)) {
                this.ShowPageError("An E-Number must be provided when creating a student assignment.");
                return this.RedirectToAction<BoxesController>(c => c.Modify(BoxNumber));
            }

            tblPOBox pobox = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == BoxNumber).SingleOrDefault();
            Regex regENumber = new Regex(@"^[Ee][0-9]{8}$");    // Reg-Ex for E########
            if (pobox != null && pobox.BoxTypeID == DataConstants.BoxTypeValues.Student) {
                SecureAccess.Utilities.Banner.User user = null;
                if (regENumber.IsMatch(ENumberOrUsername))
                    user = ENumberOrUsername.GetUserByENumber();
                else
                    user = ENumberOrUsername.GetUserByADUsername();

                if (user != null) {
                    // look for active student assignments for the user's ENumber.  If exists: warn, otherwise: assign.
                    if (_POBoxDB.tblStudentAssignmentDetails.Where(e => e.ENumber.ToUpper() == user.ENumber.ToUpper() && e.tblAssignment.IsActive).Count() > 0) {
                        this.ShowPageError(String.Format("A student box assignment already exists for ENumber {0}", user.ENumber));
                    }
                    else {
                        string errors = DBHelper.AssignStudentBox(user.ENumber, this.Request.RequestContext, null,BoxNumber);
                        if (string.IsNullOrEmpty(errors)) {
                            tblStudentAssignmentDetail boxDetails = _POBoxDB.tblStudentAssignmentDetails.Where(e => e.ENumber.ToUpper() == user.ENumber.ToUpper() && e.tblAssignment.IsActive).FirstOrDefault();
                            this.ShowPageMessage(string.Format("Student {0} has been assigned to PO Box {1}.", user.ENumber, boxDetails.tblAssignment.BoxNumber));
                        }
                        else
                            this.ShowPageError(errors);
                    }
                }
                else 
                    this.ShowPageError(String.Format("Could not retrieve user by the specified ENumber or Username: {0}", ENumberOrUsername));
                
            }
            else
                this.ShowPageError("A Student PO Box could not be retrieved for the specified BoxNumber.");

            return this.RedirectToAction<BoxesController>(c => c.Modify(BoxNumber));
        }


        [HttpPost]
        public ActionResult CreateDepartmentAssignment(int BoxNumber, string DepartmentName, string ContactPhone, string EmailAddress) {
            if (BoxNumber <= 0) {
                this.ShowPageError("A box number must be provided when creating a department assignment.");
                return this.RedirectToAction<BoxesController>(c => c.Index());
            }

            tblPOBox pobox = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == BoxNumber).SingleOrDefault();

            if (string.IsNullOrEmpty(DepartmentName)) {
                this.ShowPageError("A department name must be provided when creating a department assignment.");
                ViewBag.DepartmentName = DepartmentName;
                ViewBag.ContactPhone = ContactPhone;
                ViewBag.EmailAddress = EmailAddress;
                BoxDetailsViewModel Model = new BoxDetailsViewModel(pobox);
                return View("Modify",Model);
            }

            if (pobox != null && pobox.BoxTypeID == DataConstants.BoxTypeValues.Departmental) {
                bool success = _POBoxDB.AssignDepartmentalBox(BoxNumber, DepartmentName, ContactPhone, EmailAddress);
                if (success)
                    this.ShowPageMessage(String.Format("Box Number {0} was successfully assigned to {1}", BoxNumber, DepartmentName));
                else
                    this.ShowPageError(String.Format("An error occurred when attempting to assign Box Number {0} to {1}", BoxNumber, DepartmentName));
            }
            else
                this.ShowPageError("A Departmental PO Box could not be retrieved for the specified BoxNumber.");

            return this.RedirectToAction<BoxesController>(c => c.Modify(BoxNumber));
        }


        [HttpPost]
        public ActionResult CreateRentalAssignment(int BoxNumber, string ContactName, string Email, string ContactPhone, DateTime? DueDate, decimal? Fee) {
            if (BoxNumber <= 0) {
                this.ShowPageError("A box number must be provided when creating a rental assignment.");
                return this.RedirectToAction<BoxesController>(c => c.Index());
            }

            tblPOBox pobox = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == BoxNumber).SingleOrDefault();

            if (string.IsNullOrEmpty(ContactName) || !DueDate.HasValue || !Fee.HasValue) {
                this.ShowPageError("First name, due date, and fee must be provided when creating a rental assignment.");
                ViewBag.ContactName = ContactName;
                ViewBag.Email = Email;
                ViewBag.ContactPhone = ContactPhone;
                ViewBag.DueDate = DueDate;
                ViewBag.Fee = Fee;
                BoxDetailsViewModel Model = new BoxDetailsViewModel(pobox);
                return View("Modify", Model);
            }
            
            if (pobox != null && pobox.BoxTypeID == DataConstants.BoxTypeValues.Rental) {
                bool success = _POBoxDB.AssignRentalBox(BoxNumber, ContactName, Email, ContactPhone, DueDate.Value, Fee.Value);
                if (success)
                    this.ShowPageMessage(String.Format("Box Number {0} was successfully assigned to rental {1}", BoxNumber, ContactName));
                else
                    this.ShowPageError(String.Format("An error occurred when attempting to assign Box Number {0} to rental {1}", BoxNumber, ContactName));
            }
            else
                this.ShowPageError("A Rental PO Box could not be retrieved for the specified BoxNumber.");

            return this.RedirectToAction<BoxesController>(c => c.Modify(BoxNumber));
        }


        [DynamicFormAction]
        public ActionResult ExportSelected(FormCollection collection) {
            List<tblPOBox> boxList = new List<tblPOBox>();
            foreach (var key in collection.Keys) {
                if (key.ToString().StartsWith("Box_")) {
                    string boxNumberText = key.ToString().Replace("Box_", "");
                    if (!string.IsNullOrEmpty(boxNumberText)) {
                        try {
                            int boxNumber = Int32.Parse(boxNumberText);
                            tblPOBox box = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == boxNumber).SingleOrDefault();
                            if (box != null) {
                                boxList.Add(box);
                            }
                        }
                        catch (Exception ex) {
                            Logging.LogException(Logging.LogType.Audit, "An error occurred when attempting to toggle assignability of a po box", ex);
                        }
                    }
                }
            }
            List<BoxSummary> output = boxList.Select(e => new BoxSummary(e)).ToList();
            return new CSVResult<BoxSummary>(output) { FileDownloadName = "POBoxListResults.csv" };            
        }

        [DynamicFormAction]
        public ActionResult ExportSelectedForwards(FormCollection collection) {
            List<tblPOBox> boxList = new List<tblPOBox>();
            foreach (var key in collection.Keys) {
                if (key.ToString().StartsWith("Box_")) {
                    string boxNumberText = key.ToString().Replace("Box_", "");
                    if (!string.IsNullOrEmpty(boxNumberText)) {
                        try {
                            int boxNumber = Int32.Parse(boxNumberText);
                            tblPOBox box = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == boxNumber).SingleOrDefault();
                            if (box != null) {
                                boxList.Add(box);
                            }
                        }
                        catch (Exception ex) {
                            Logging.LogException(Logging.LogType.Audit, "An error occurred when attempting to toggle assignability of a po box", ex);
                        }
                    }
                }
            }
            List<DymoForwardExport> output = boxList.Select(e => new DymoForwardExport(e)).ToList();
            return new CSVResult<DymoForwardExport>(output) { FileDownloadName = "POBoxListExportSelectedForwards.csv" };
        } 

        [DynamicFormAction]
        public ActionResult ToggleAssignability(FormCollection collection) {

            bool hasPendingChanges = false;
            foreach (var key in collection.Keys) {
                if (key.ToString().StartsWith("Box_")) {
                    string boxNumberText = key.ToString().Replace("Box_", "");
                    if (!string.IsNullOrEmpty(boxNumberText)) {
                        try {
                            int boxNumber = Int32.Parse(boxNumberText);
                            tblPOBox box = _POBoxDB.tblPOBoxes.Where(e => e.BoxNumber == boxNumber).SingleOrDefault();
                            if (box != null) {
                                box.IsAssignable = !box.IsAssignable;
                                hasPendingChanges = true;
                                Logging.Log(Logging.LogType.Audit, String.Format("Updating assignability for box {0} to value: {1}", boxNumber,box.IsAssignable));
                            }
                        }
                        catch (Exception ex) {
                            Logging.LogException(Logging.LogType.Audit, "An error occurred when attempting to toggle assignability of a po box", ex);
                        }
                    }
                }
            }

            if (hasPendingChanges) {
                _POBoxDB.SaveChanges();
                this.ShowPageMessage("Box assignability was successfully updated");
            }

            return this.RedirectToAction<BoxesController>(c => c.Index());
        }


    } // end class
}  // end namespace
