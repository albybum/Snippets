using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Orientation.Models;
using SecureAccess.Utilities;
using Orientation.Models.ViewModels;
using Orientation.Classes;
using System.Web.Security;
using System.Data;
using Orientation.Models.BannerDTOs;

namespace Orientation.Controllers
{ 

    [Authorize(Order = 2)]
    [ActiveSessionUser(Order = 1)]
    public class StudentController : ControllerBase
    {
        private OrientationDB _ODB = new OrientationDB();

        //
        // GET: /Student/

        public ActionResult Index()
        {
            return View(_ODB.tblInvitations.Where(e => e.ENumber == Current.User.ENumber).ToList());
        }

        /// <summary>
        /// Displays Event Invitation Details to Student
        /// </summary>
        /// <param name="ID">Invitation ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult Invitation(int ID) {
            tblInvitation StudentInvitation = _ODB.tblInvitations.Where(e => e.ID == ID).SingleOrDefault();
            if (StudentInvitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                Logging.Log(Logging.LogType.Audit, string.Format("User {1} with ENumber {0} is viewing Student Event Invitation with ID {2} for event '{3}'", Current.User.ENumber, Current.User.Username, ID, StudentInvitation.tblEvent.Name));
                return View(StudentInvitation);
            }
            else {
                this.ShowPageError("You do not have permission to access that Invitation");
                return this.RedirectToAction<StudentController>(c => c.Index());
            }
        }

        /// <summary>
        /// Displays Reservation Details to Student
        /// </summary>
        /// <param name="ID">Reservation ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult Reservation(int ID) {
            tblReservation Reservation = _ODB.tblReservations.Where(e => e.ID == ID).SingleOrDefault();
            if (Reservation.tblInvitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                ViewBag.MaxNumberGuests = Reservation.tblEventDate.MaxNumberGuests;
                Logging.Log(Logging.LogType.Audit, string.Format("User {1} with ENumber {0} is viewing Student Reservation with ID {2} for event '{3}'", Current.User.ENumber, Current.User.Username, ID, Reservation.tblInvitation.tblEvent.Name));
                BannerDB Banner = new BannerDB();
                bool IsBannerUp = Banner.Ping();
                if (IsBannerUp) {
                    ViewBag.IsBannerUp = IsBannerUp;
                    ViewBag.HasSelectiveServiceHold = Banner.HasSelectiveServiceHold(Reservation.tblInvitation.ENumber);
                    ViewBag.MustVerifyCitizenship = Banner.MustVerifyCitizenship(Reservation.tblInvitation.ENumber);
                    ViewBag.FinAidRequirements = Banner.GetFinAidChecklistRequirements(Reservation.tblInvitation.ENumber);
                    ViewBag.FAFSAAidYear = Banner.GetFAFSAAidYear(Reservation.tblInvitation.ENumber);
                    ViewBag.ImmunizationList = Banner.GetImmunizationsChecklist(Reservation.tblInvitation.ENumber);
                    List<TestScore> TestScores = Banner.GetTestScores(Reservation.tblInvitation.ENumber).AsEnumerable().Select(e => new TestScore(e)).ToList();
                    TestScore DSPR = TestScores.Where(e => e.Code == "DSPR").OrderByDescending(e => e.Date).FirstOrDefault();
                    TestScore DSPW = TestScores.Where(e => e.Code == "DSPW").OrderByDescending(e => e.Date).FirstOrDefault();
                    TestScore DSPM = TestScores.Where(e => e.Code == "DSPM").OrderByDescending(e => e.Date).FirstOrDefault();
                    if (DSPR == null || DSPW == null || DSPM == null || DSPR.Score.Trim() != "4" || DSPW.Score.Trim() != "4" || DSPM.Score.Trim() != "4")
                        ViewBag.HasLearningSupportRequirements = true;
                    else
                        ViewBag.HasLearningSupportRequirements = false;
                }
                return View(Reservation);
            }
            else {
                this.ShowPageError("You do not have permission to access that Reservation");
                return this.RedirectToAction<StudentController>(c => c.Index());
            }
        }

        /// <summary>
        /// Student Payment Choice page, caclualtes and displays any pending charges for reservation. Allows student to select Payment Method
        /// </summary>
        /// <param name="ID">Reservation ID</param>
        /// <returns></returns>
        public ActionResult Payment(int ID) {
            tblReservation Reservation = _ODB.tblReservations.Where(e => e.ID == ID).SingleOrDefault();
            if (Reservation.tblInvitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                ViewBag.MaxNumberGuests = Reservation.tblEventDate.MaxNumberGuests;
                Logging.Log(Logging.LogType.Audit, string.Format("User {1} with ENumber {0} is viewing Student Payment Page with Reservation ID {2} for event '{3}'", Current.User.ENumber, Current.User.Username, ID, Reservation.tblInvitation.tblEvent.Name));
                return View(Reservation);
            }
            else {
                this.ShowPageError("You do not have permission to access that Payment");
                return this.RedirectToAction<StudentController>(c => c.Index());
            }
        }

        /// <summary>
        /// Creates placeholder credit card payment/transaction and sends the user to TouchNet to process the payment
        /// </summary>
        /// <param name="ID">Reservation ID</param>
        /// <returns>Redirect ActionResult</returns>
        public ActionResult CreditPayment(int ID) {
            tblReservation Reservation = _ODB.tblReservations.Where(e => e.ID == ID).SingleOrDefault();
            if (Reservation != null) {
                bool HasExistingPayments = Reservation.tblPayments.Where(e => e.Amount > 0).Count() > 0;
                bool IsStudentPayment = Reservation.tblInvitation.ENumber == Current.User.ENumber;
                if (Reservation.tblInvitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                    if (Reservation.CurrentAmountDue > 0) {
                        if (Reservation.tblEventDate.HasOpenSlots || (Reservation.IsConfirmed && Reservation.CountUnpaidGuests > 0)) {
                            // create placeholder payment to store data on upay postback
                            tblPayment Payment = new tblPayment();
                            Payment.tblReservation = Reservation;
                            Payment.IsSupplemental = HasExistingPayments;
                            Payment.PaidBy = IsStudentPayment ? "Student" : "Staff";
                            Payment.PaymentType = DataConstants.PaymentTypes.CreditOrDebitCard;
                            Payment.Amount = 0; // don't set amount until received
                            Payment.PaymentDate = DateTime.Now;
                            _ODB.tblPayments.AddObject(Payment); _ODB.SaveChanges();

                            Current.User.BookmarkReservationID = Reservation.ID; // remember which reservation we're using so we can return the user to the correct page.

                            // Create a placeholder transaction to store credit card data
                            tblUPayTransaction transaction = Upay.CreateTouchnetTransaction(Payment);

                            SecureAccess.Utilities.Logging.Log(SecureAccess.Utilities.Logging.LogType.Audit, string.Format("Redirecting user {0} to {1} UPay site.", Current.User.Username, Reservation.tblInvitation.tblEvent.MarketplaceSite), SecureAccess.Utilities.Logging.Severity.Information);
                            Upay.PostToTouchnet(transaction, Reservation.CurrentAmountDue, HasExistingPayments, Payment.PaidBy);
                            return null;
                        }
                        else
                            this.ShowPageError(string.Format("The selected event date '{0}' no longer has an open slot for event '{1}'. Please select another event date.",Reservation.tblEventDate.DateOfEvent.ToShortDateString(),Reservation.tblEventDate.tblEvent.Name));
                    }
                    else {
                        this.ShowPageError(String.Format("You cannot submit a credit card payment. The Current Amount Due is {0}", Reservation.CurrentAmountDue));
                    }

                    return this.RedirectToAction<StudentController>(c => c.Reservation(ID));
                }
                else
                    this.ShowPageError("You are not authorized to submit credit payments for this Reservation");
            }
            else
                this.ShowPageError("Could not find a reservation for the given ID");

            return this.RedirectToAction<StudentController>(c => c.Index());
        }

        /// <summary>
        /// Confirms selected reservation if no payments are due
        /// </summary>
        /// <param name="ID">Reservation ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult ConfirmReservation(int ID) {
            tblReservation Reservation = _ODB.tblReservations.Where(e => e.ID == ID).SingleOrDefault();
            if (Reservation != null) {
                bool HasExistingPayments = Reservation.tblPayments.Where(e => e.Amount > 0).Count() > 0;
                bool IsStudentPayment = Reservation.tblInvitation.ENumber == Current.User.ENumber;
                if (Reservation.tblInvitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                    if (Reservation.CurrentAmountDue <= 0) {
                        if (Reservation.tblEventDate.HasOpenSlots) {
                            Reservation.IsConfirmed = true; // mark reservation as confirmed
                            Reservation.DateConfirmed = DateTime.Now;
                            Reservation.tblGuests.ToList().ForEach(e => e.HasPaid = true); // mark current guests as paid
                            _ODB.SaveChanges();

                            if (Reservation.tblEventDate.AutomaticallyClearSOHolds) {
                                BannerDB Banner = new BannerDB();
                                if (Banner.Ping()) {
                                    Banner.ReleaseHold(Reservation.tblInvitation.ENumber, DataConstants.HoldTypes.StudentOrientation);
                                    Logging.Log(Logging.LogType.Audit, string.Format("User {0} Released Hold Code '{1}' for Student {2}", Current.User.Username, DataConstants.HoldTypes.StudentOrientation, Reservation.tblInvitation.ENumber));
                                }
                                else
                                    Logging.Log(Logging.LogType.Audit, String.Format("Could not connect to Banner to release SO Hold for student {0} for Reservation {1}", Reservation.tblInvitation.ENumber, Reservation.ID), "Holds", Logging.Severity.Warning);
                            }

                            if (Reservation.tblEventDate.UpdateSGBSTDN_ORSN) {
                                try {
                                    (new BannerDB()).UpdateSGBSTDN_ORSN_Code(Reservation.tblInvitation.ENumber, Reservation.tblEventDate.EventDateType);
                                    Logging.Log(Logging.LogType.Audit, string.Format("Successfully updated SGBSTDN ORSN code '{0}' for Reservation ID {1}. Student E-Number {2}", Reservation.tblEventDate.EventDateType, Reservation.ID, Reservation.tblInvitation.ENumber));
                                }
                                catch (Exception ex) {
                                    Logging.LogException(Logging.LogType.Audit, string.Format("StudentController->ConfirmReservation(): An exception occurred while trying to update SGBSTDN ORSN code '{0}' for Reservation ID {1}. Student E-Number {2}", Reservation.tblEventDate.EventDateType, Reservation.ID, Reservation.tblInvitation.ENumber), ex);
                                }
                            }

                            // try to insert row for IDCard Extract
                            try {
                                (new BannerDB()).INSERT_IDCARD_FLAG(Reservation.tblInvitation.Pidm, Reservation.tblInvitation.tblEvent.Term, "Y");
                                Logging.Log(Logging.LogType.Audit, string.Format("Successfully inserted IDCard Extract Flag for Reservation ID {0}. Student E-Number {1}", Reservation.ID, Reservation.tblInvitation.ENumber));
                            }
                            catch (Exception ex) {
                                Logging.LogException(Logging.LogType.Audit, string.Format("StudentController->ConfirmReservation(): An exception occurred while trying to insert IDCard Extract Flag for Reservation ID {0}. Student E-Number {1}", Reservation.ID, Reservation.tblInvitation.ENumber), ex);
                            }

                            this.ShowPageMessage("Your Reservation has been confirmed");
                        }
                        else
                            this.ShowPageError(string.Format("The selected event date '{0}' no longer has an open slot for event '{1}'. Please select another event date.", Reservation.tblEventDate.DateOfEvent.ToShortDateString(), Reservation.tblEventDate.tblEvent.Name));
                    }
                    else {
                        this.ShowPageError(String.Format("You must pay fees due before confirming registration. The Current Amount Due is {0}", Reservation.CurrentAmountDue));
                    }

                    return this.RedirectToAction<StudentController>(c => c.Reservation(ID));
                }
                else
                    this.ShowPageError("You are not authorized to confirm this Reservation");
            }
            else
                this.ShowPageError("Could not find a reservation for the given ID");

            return this.RedirectToAction<StudentController>(c => c.Index());
        }

        /// <summary>
        /// View current/unviewed launch page for student
        /// </summary>
        /// <param name="ID">Invitation ID</param>
        /// <returns>Action Result</returns>
        public ActionResult Launch(int ID) {
            tblInvitation Invitation = _ODB.tblInvitations.Where(e => e.ID == ID).SingleOrDefault();
            if (Invitation.ENumber == Current.User.ENumber) {
                tblLaunchPage Page = null;
                if (Invitation != null) {
                    Page = Invitation.CurrentLaunchPage;
                    return View(new Tuple<tblInvitation, tblLaunchPage>(Invitation, Page));
                }
                else
                    this.ShowPageError("Could not find an orientation invitation for the given ID");
            }
            else
                this.ShowPageError("You are not authorized to view Launch material for this Invitation");

            return this.RedirectToAction<StudentController>(c => c.Index());
        }

        /// <summary>
        /// Allows the student to review a previously viewed & submitted launch page
        /// </summary>
        /// <param name="ID">Invitation ID</param>
        /// <param name="LaunchPageID">Launch Page ID</param>
        /// <returns></returns>
        public ActionResult ReviewLaunchPage(int ID, int LaunchPageID) {
            tblInvitation Invitation = _ODB.tblInvitations.Where(e => e.ID == ID).SingleOrDefault();
            tblLaunchPage Page = null;
            if (Invitation.ENumber == Current.User.ENumber) {
                if (Invitation != null) {
                    Page = Invitation.CompletedLaunchPages.Where(e => e.ID == LaunchPageID).SingleOrDefault();
                    return View("Launch", new Tuple<tblInvitation, tblLaunchPage>(Invitation, Page));
                }
                else
                    this.ShowPageError("Could not find an orientation invitation for the given ID");
            }
            else
                this.ShowPageError("You are not authorized to view Launch material for this Invitation");

            return this.RedirectToAction<StudentController>(c => c.Index());
        }

        /// <summary>
        /// View current/incomplete quiz question for student
        /// </summary>
        /// <param name="ID">Invitation ID</param>
        /// <returns></returns>
        public ActionResult Quiz(int ID) {
            tblInvitation Invitation = _ODB.tblInvitations.Where(e => e.ID == ID).SingleOrDefault();
            if (Invitation.ENumber == Current.User.ENumber) {
                tblQuizQuestion Question = null;
                if (Invitation != null) {
                    Question = Invitation.CurrentQuizQuestion;
                    return View("Quiz", new Tuple<tblInvitation, tblQuizQuestion>(Invitation, Question));
                }
                else
                    this.ShowPageError("Could not find an orientation invitation for the given ID");
            }
            else
                this.ShowPageError("You are not authorized to view quiz material for this Invitation");

            return this.RedirectToAction<StudentController>(c => c.Index());
        }

        /// <summary>
        /// Mark the selected launch page as completed for the current student.
        /// </summary>
        /// <param name="ID">Invitation ID</param>
        /// <param name="LaunchPageID">Launch Page ID</param>
        /// <returns></returns>
        public ActionResult CompleteLaunchPage(int ID, int LaunchPageID) {
            tblInvitation Invitation = _ODB.tblInvitations.Where(e => e.ID == ID).SingleOrDefault();
            tblLaunchPage LaunchPage = _ODB.tblLaunchPages.Where(e => e.ID == LaunchPageID).SingleOrDefault();
            tblCompletedLaunchPage existingPage = Invitation.tblCompletedLaunchPages.Where(e => e.LaunchPageID == LaunchPageID).SingleOrDefault();
            if (Invitation.ENumber == Current.User.ENumber || Current.User.IsAdmin) {
                if (existingPage == null) {
                    tblCompletedLaunchPage CompletedPage = new tblCompletedLaunchPage();
                    CompletedPage.tblInvitation = Invitation;
                    CompletedPage.tblLaunchPage = LaunchPage;
                    CompletedPage.DateCompleted = DateTime.Now;
                    _ODB.tblCompletedLaunchPages.AddObject(CompletedPage);
                    _ODB.SaveChanges();
                    Logging.Log(Logging.LogType.Audit, string.Format("User {0} completed Launch Page ID {1} for Invitation {2}. tblCompletedLaunchPage.ID = {3}",Current.User.Username,LaunchPage.ID,Invitation.ID,CompletedPage.ID));
                    if (Invitation.HasPendingLaunch)
                        return this.RedirectToAction<StudentController>(c => c.Launch(Invitation.ID));
                    else {
                        this.ShowPageMessage("You have completed the required LAUNCH module.");
                        return this.RedirectToAction<StudentController>(c => c.Invitation(Invitation.ID));
                    }
                }
                else
                    this.ShowPageError("The submitted launch page has already been completed.");
            }
            else
                this.ShowPageError("You are not authorized to view Launch material for this Invitation");

            return this.RedirectToAction<StudentController>(c => c.Launch(Invitation.ID));
        }

        /// <summary>
        /// Check the student's quiz question answer and save progress if correct.
        /// </summary>
        /// <param name="ID">Invitation ID</param>
        /// <param name="QuizQuestionID">Quiz Question ID</param>
        /// <param name="AnswerID">Answer ID</param>
        /// <returns></returns>
        public ActionResult CompleteQuizQuestion(int ID, int QuizQuestionID, int AnswerID) {
            tblInvitation Invitation = _ODB.tblInvitations.Where(e => e.ID == ID).SingleOrDefault();
            tblQuizQuestion Question = _ODB.tblQuizQuestions.Where(e => e.ID == QuizQuestionID).SingleOrDefault();
            tblQuizQuestionAnswer Answer = Question.tblQuizQuestionAnswers.Where(e => e.ID == AnswerID).SingleOrDefault();
            tblCompletedQuizQuestion existingAnswer = Invitation.tblCompletedQuizQuestions.Where(e => e.QuizQuestionID == QuizQuestionID).SingleOrDefault();
            if (Invitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                if (existingAnswer == null) {
                    if (AnswerID != -1 && Answer != null) {
                        if (Answer.Correct) {
                            tblCompletedQuizQuestion CompletedQuestion = new tblCompletedQuizQuestion();
                            CompletedQuestion.tblInvitation = Invitation;
                            CompletedQuestion.tblQuizQuestion = Question;
                            CompletedQuestion.DateCompleted = DateTime.Now;
                            _ODB.tblCompletedQuizQuestions.AddObject(CompletedQuestion);
                            _ODB.SaveChanges();
                            Logging.Log(Logging.LogType.Audit, string.Format("User {0} completed Quiz Question ID {1} for Invitation {2}. tblCompletedQuizQuestion.ID = {3}", Current.User.Username, Question.ID, Invitation.ID, CompletedQuestion.ID));
                            if (Invitation.HasPendingQuiz) {
                                this.ShowPageMessage(string.Format("Correct! The answer was '{0}'. Please answer the next question", Answer.Answer));
                                return this.RedirectToAction<StudentController>(c => c.Quiz(Invitation.ID));
                            }
                            else {
                                this.ShowPageMessage("You have completed the required Quiz.");
                                return this.RedirectToAction<StudentController>(c => c.Invitation(Invitation.ID));
                            }
                        }
                        else
                            this.ShowPageError(string.Format("The answer you provided was not correct: '{0}'. Please choose another answer.", Answer.Answer));
                    }
                    else
                        this.ShowPageError("Could not find an answer with the submitted values. Please choose an answer. If no answer choices are available for this question, please contact: orientation@etsu.edu");
                }
                else
                    this.ShowPageError("The submitted quiz question has already been completed.");
            }
            else
                this.ShowPageError("You are not authorized to view Quiz material for this Invitation");

            return this.RedirectToAction<StudentController>(c => c.Quiz(Invitation.ID));
        }

        /// <summary>
        /// Creates a Reservation given a student's invitation ID and the selected Event Date ID
        /// </summary>
        /// <param name="ID">Invitation ID</param>
        /// <param name="EventDateID">Event Date ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult CreateReservation(int ID, int EventDateID) {
            tblInvitation Invitation = _ODB.tblInvitations.Where(e => e.ID == ID).SingleOrDefault();
            tblEventDate EventDate = _ODB.tblEventDates.Where(e => e.ID == EventDateID).SingleOrDefault();
            if (Invitation != null && EventDate != null) {
                if (Invitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                    tblReservation existingReservation = _ODB.tblReservations.Where(e => e.InvitationID == Invitation.ID).SingleOrDefault();
                    if (existingReservation == null) {
                        if (EventDate.HasOpenSlots) {
                            tblReservation Reservation = new tblReservation();
                            Reservation.tblEventDate = EventDate;
                            Reservation.tblInvitation = Invitation;
                            Reservation.IsCancelled = false;
                            Reservation.IsConfirmed = false;
                            _ODB.tblReservations.AddObject(Reservation);
                            _ODB.SaveChanges();
                            Logging.Log(Logging.LogType.Audit, string.Format("User {0} created a Reservation with ID {1} for Invitation {2} for EventDate {3}", Current.User.Username, Reservation.ID, Invitation.ID, EventDate.ID));
                            this.ShowPageMessage("Your event reservation was successfully created!");
                            return this.RedirectToAction<StudentController>(c => c.Reservation(Reservation.ID));
                        }
                        else
                            this.ShowPageError(string.Format("The selected event date '{0}' no longer has an open slot for event '{1}'. Please select another event date.", existingReservation.tblEventDate.DateOfEvent.ToShortDateString(), existingReservation.tblEventDate.tblEvent.Name));
                    }
                    else
                        this.ShowPageError("A reservation already exists for the selected Invitation. Please update the reservation to change event dates.");
                }
                else
                    this.ShowPageError("You are not authorized to view create reservations for this Invitation");
            }
            else
                this.ShowPageError("Either the Invitation or the Event Date could not be retrieved given the specified IDs.");

            if (Invitation != null)
                return this.RedirectToAction<StudentController>(c => c.Invitation(Invitation.ID));
            else
                return this.RedirectToAction<StudentController>(c => c.Index());
        }

        /// <summary>
        /// Change the event date for a given reservation and new event id
        /// </summary>
        /// <param name="ID">Reservation ID</param>
        /// <param name="EventDateID">Event Date ID</param>
        /// <returns>ActionResult</returns>
        public ActionResult ChangeReservation(int ID, int EventDateID) {
            tblReservation Reservation = _ODB.tblReservations.Where(e => e.ID == ID).SingleOrDefault();
            tblInvitation Invitation = Reservation != null ? Reservation.tblInvitation : null;
            tblEventDate EventDate = _ODB.tblEventDates.Where(e => e.ID == EventDateID).SingleOrDefault();
            if (Invitation != null && EventDate != null) {
                if (Invitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                    if (Reservation != null) {
                        if (EventDate.HasOpenSlots) {
                            // delete unpaid guests to avoid conflicts with the number of allowed guests with different event dates, but yet preserve paid guests
                            List<tblGuest> GuestsToDelete = Reservation.tblGuests.Where(e => !e.HasPaid).ToList();
                            GuestsToDelete.ForEach(e => _ODB.tblGuests.DeleteObject(e));
                            Reservation.tblEventDate = EventDate;
                            _ODB.SaveChanges();
                            Logging.Log(Logging.LogType.Audit, string.Format("User {0} Changed the Event Date for Reservation with ID {1} for Invitation {2} for EventDate {3}", Current.User.Username, Reservation.ID, Invitation.ID, EventDate.ID));
                            this.ShowPageMessage("Your event date for your reservation was successfully changed!");
                            return this.RedirectToAction<StudentController>(c => c.Reservation(Reservation.ID));
                        }
                        else {
                            this.ShowPageError(string.Format("The selected event date '{0}' no longer has an open slot for event '{1}'. Please select another event date.", Reservation.tblEventDate.DateOfEvent.ToShortDateString(), Reservation.tblEventDate.tblEvent.Name));
                            return this.RedirectToAction<StudentController>(c => c.Reservation(Reservation.ID));
                        }
                    }
                    else
                        this.ShowPageError("A reservation could not be found by the given ID.");
                }
                else
                    this.ShowPageError("You are not authorized to view create reservations for this Invitation");
            }
            else
                this.ShowPageError("Either the Invitation or the Event Date could not be retrieved given the specified IDs.");

            if (Invitation != null)
                return this.RedirectToAction<StudentController>(c => c.Invitation(Invitation.ID));
            else
                return this.RedirectToAction<StudentController>(c => c.Index());
        }

        /// <summary>
        /// Adds a guest to a student event reservation
        /// </summary>
        /// <param name="Model">Guest View Model</param>
        /// <returns>ActionResult</returns>
        public ActionResult AddGuest(GuestViewModel Model) {
            tblReservation Reservation = _ODB.tblReservations.Where(e => e.ID == Model.ReservationID).SingleOrDefault();
            if (Reservation != null) {
                if (Reservation.tblInvitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                    if (ModelState.IsValid) {
                        if (Reservation.tblGuests.Count < Reservation.tblEventDate.MaxNumberGuests) {
                            if (Reservation.tblEventDate.AllowGuestRegistration) {
                                tblGuest existingGuest = Reservation.tblGuests.Where(e => e.EmailAddress.ToUpper() == Model.EmailAddress.ToUpper()).FirstOrDefault();
                                if (existingGuest == null) {
                                    tblGuest newGuest = Model.As_tblGuest();
                                    newGuest.tblReservation = Reservation;
                                    _ODB.tblGuests.AddObject(newGuest);
                                    _ODB.SaveChanges();
                                    Logging.Log(Logging.LogType.Audit, string.Format("User {0} has added new guest with email '{1}', Guest ID={2} to reservation with ID {3}", Current.User.Username, newGuest.EmailAddress, newGuest.ID, Reservation.ID));
                                    if (Reservation.IsConfirmed)
                                        this.ShowPageMessage(string.Format("Successfully added guest '{0}, {1} {2}' with email '{3}' to reservation. Your reservation has already been confirmed.  Please be sure to pay any additional fees that may be related to the additional guests you have added.", newGuest.LastName, newGuest.FirstName, newGuest.MiddleName, newGuest.EmailAddress));
                                    else
                                        this.ShowPageMessage(string.Format("Successfully added guest '{0}, {1} {2}' with email '{3}' to reservation.", newGuest.LastName, newGuest.FirstName, newGuest.MiddleName, newGuest.EmailAddress));

                                    return this.RedirectToAction<StudentController>(c => c.Reservation(Reservation.ID));
                                }
                                else
                                    this.ShowPageError(string.Format("A guest has already been added to this reservation with the provided email address: {0}.", Model.EmailAddress));
                            }
                            else
                                this.ShowPageError("The selected event date for this reservation does not allow guest registration.");
                        }
                        else
                            this.ShowPageError(string.Format("You have already added the maximum number of guests. Guest Count: {0}. Max Guests: {1}", Reservation.tblGuests.Count, Reservation.tblEventDate.MaxNumberGuests));
                    }
                    else
                        this.ShowPageError(DataConstants.ModelStateInvalidError);
                }
                else
                    this.ShowPageError("You are not authorized to add guests to this reservation");
            }
            else
                this.ShowPageError("Reservation could not be found for the given ID. No guest was added.");

            ViewBag.ExpandAddGuestSection = true;
            ViewBag.GuestModel = Model;
            return View("Reservation",Reservation);
        }

        /// <summary>
        /// (Ajax): Displays a list of guests for a given Reservation
        /// </summary>
        /// <param name="ID">Reservation ID</param>
        /// <returns>PartialViewResult</returns>
        [HttpAjaxRequest]
        public PartialViewResult GuestList(int ID) {
            tblReservation Reservation = _ODB.tblReservations.Where(e => e.ID == ID).SingleOrDefault();
            if (Reservation != null) {
                if (Reservation.tblInvitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                    ViewBag.MaxNumberGuests = Reservation.tblEventDate.MaxNumberGuests;
                    return PartialView(Reservation.tblGuests.ToList());
                }
                else
                    Logging.Log(Logging.LogType.Audit, string.Format("User {0} does not have permission to access the guest list for Reservation with ID {1}",Current.User.Username,Reservation.ID));
            }
            return PartialView(new List<tblGuest>());
        }

        /// <summary>
        /// (Ajax): Deletes a guest from a reservation by guest ID
        /// </summary>
        /// <param name="ID">Guest ID</param>
        /// <returns>status message string</returns>
        [HttpAjaxRequest]
        public string DeleteGuest(int ID) {
            string retVal = string.Empty;
            tblGuest guest = _ODB.tblGuests.Where(e => e.ID == ID).SingleOrDefault();
            if (guest != null) {
                if (guest.tblReservation.tblInvitation.ENumber == Current.User.ENumber || Current.User.IsAdmin || Current.User.IsStaff) {
                    Logging.Log(Logging.LogType.Audit,string.Format("User {0} has chosen to delete guest with ID {1} and email '{2}'",Current.User.Username,guest.ID, guest.EmailAddress));
                    _ODB.tblGuests.DeleteObject(guest);
                    _ODB.SaveChanges();
                    retVal = "The selected guest was deleted successfully.";
                }
                else
                    retVal = "You are not authorized to delete guests from this reservation";
            }
            else 
                retVal = "The selected guest could not be deleted. The system could not find the guest by the provided ID";

            return retVal;
        }


        public ActionResult UPayError() {
            TempData["UpayError"] = "An Error Occurred During The Credit or Debit Card Transaction. If the problem persists or you are unsure whether your payment has been received, please contact the Student Affairs Division Office First Year Programs. orientation@etsu.edu";
            FormsAuthentication.SignOut();
            Session.Clear();
            SecureAccess.Utilities.Logging.Logout();
            return View("Error");
        }

        public ActionResult UPayCancel() {
            if (!Current.User.BookmarkReservationID.HasValue)
                return this.RedirectToAction<DefaultController>(c => c.Index()); // no bookmark id, go to default controller index
            else
                return this.RedirectToAction<StudentController>(c => c.Payment(Current.User.BookmarkReservationID.Value)); // redirect student to student payment view
        }

        public ActionResult Confirmation() {
            this.ShowPageMessage("Your payment has been submitted.");

            if (!Current.User.BookmarkReservationID.HasValue)
                return this.RedirectToAction<DefaultController>(c => c.Index()); // no bookmark id, go to default controller index
            else {
                tblReservation Reservation = _ODB.tblReservations.Where(e => e.ID == Current.User.BookmarkReservationID.Value).SingleOrDefault();
                if (Reservation == null) {
                    return this.RedirectToAction<DefaultController>(c => c.Index()); // couldn't find reservation, go to default controller index
                }
                else {
                    if (Reservation.tblInvitation.ENumber == Current.User.ENumber) {
                        return this.RedirectToAction<StudentController>(c => c.Payment(Current.User.BookmarkReservationID.Value)); // redirect student to student payment view
                    }
                    else if (Current.User.IsAdmin || Current.User.IsStaff) {
                        return this.RedirectToAction<InvitationsController>(c => c.Invitation(Reservation.tblInvitation.ID)); // redirect staff back to Invitation details
                    }
                    else {
                        return this.RedirectToAction<DefaultController>(c => c.Index()); // unknown role, go to default controller index
                    }
                }
            }
            
        }

    }
}
