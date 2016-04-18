using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ORSPARouting.Models.ViewModels;
using ORSPARouting.Models;
using System.IO;
using ORSPARouting.Classes;
using ORSPARouting.Classes.Workflow;
using SecureAccess.Utilities;

namespace ORSPARouting.Controllers
{
    [Authorize(Order = 3)]
    [AuthorizeUser(Order = 2)]
    [ActiveSessionUser(Order = 1)]
    public class RequestController : ControllerBase
    {
        //
        // GET: /Form/

        /// <summary>
        /// Get read-only form for given ID
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public ActionResult Index(int? ID, int? StepID) {
            FormViewModel Model = null;
            tblForm ExistingForm;

            // validate input
            if(ID == null || ID.Value <= 0) {
                TempData[ORSPARouting.Models.DataConstants.ErrorKey] = ORSPARouting.Models.DataConstants.FormNotFoundError;
                return View();            
            }

            ExistingForm = (new FormsDataContext()).tblForms.Where(e=>e.ID == ID.Value).SingleOrDefault(); // get existing form

            if (ExistingForm != null) {
                Model = new FormViewModel(ExistingForm);
                Model.ReadOnly = true;

                //set accordion index
                if (StepID != null && Model.WorkflowData.Workflow != null) {
                    int Index = 0;
                    foreach (Workflow.Step Step in Model.WorkflowData.Workflow.GetSteps().Where(s => s.StepType == "Action" && s.Valid)) {
                        if (Step.ID == StepID.Value) { this.SetIndex(Index); break; } else { Index += 1; }
                    }
                }

                return View(Model);
            }
            else {
                TempData[ORSPARouting.Models.DataConstants.ErrorKey] = ORSPARouting.Models.DataConstants.FormNotFoundError;
                return View();
            }
        }

        /// <summary>
        /// Select type of Form to create.
        /// </summary>
        /// <returns></returns>
        public ActionResult Select(int ProjectID) {
            FormSelectionViewModel Model = new FormSelectionViewModel(ProjectID);
            return View(Model);
        }


        /// <summary>
        /// Display a structured form based on selected form type. Fills out form if previous form data is available.
        /// </summary>
        /// <param name="SelectionModel"></param>
        /// <returns></returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult New(FormSelectionViewModel SelectionModel) {
            FormViewModel Model = null;
            if (ModelState.IsValid) {
                if (SelectionModel.FormType == DataConstants.FormType.ContractFromPreviousProposal) {
                    // validate Contract From Previous Proposal - make sure there is a proposal selected.
                    if (SelectionModel.SelectedFormID <= 0) {
                        this.ShowPageError("You must select a previous proposal to create a Contract From Previous Proposal.");
                        return RedirectToAction("Select",new {@ProjectID = SelectionModel.ProjectID});
                    }
                }

                Model = new FormViewModel(SelectionModel);
            }
            else {
                TempData[ORSPARouting.Models.DataConstants.ErrorKey] = ORSPARouting.Models.DataConstants.ModelStateInvalidError;
                return View("Select", SelectionModel);
            }

            return View(Model);
        }

        /// <summary>
        /// Shows the edit form for a request form, by given id
        /// </summary>
        /// <param name="id">Form ID</param>
        /// <returns></returns>
        public ActionResult Edit(int id) {
            FormsDataContext FormsDB = new FormsDataContext();
            tblForm ExistingForm = FormsDB.tblForms.Where(e => e.ID == id).SingleOrDefault();
            FormViewModel Model = null;
            if (ExistingForm != null) {
                Model = new FormViewModel(ExistingForm);
            }
            else {
                this.ShowPageError(string.Format("Could not find a Request form for the given id: {0}", id));
                return RedirectToAction("Status", "Projects");
            }

            return View(Model);
        }


        /// <summary>
        /// Creates / Submits the form data
        /// </summary>
        /// <param name="Model"></param>
        /// <returns></returns>
        [DynamicFormAction]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Create(FormViewModel Model) {
            FormsDataContext FormsDB = new FormsDataContext();
            tblForm ExistingForm = FormsDB.tblForms.Where(e => e.ID == Model.ID).SingleOrDefault();
            if (ExistingForm != null) Model.ReloadExistingDocuments(ExistingForm);

            if (ModelState.IsValid) {
                if (CustomRequestValidation(Model)) {
                    tblForm Form = null;
                    // Create form DB object from Model
                    try {
                        Form = new tblForm(Model);
                    }
                    catch (Exception ex) {
                        TempData[ORSPARouting.Models.DataConstants.ErrorKey] = "An error occurred when attempting to save the form. The values were not saved. The error has been logged.";
                        TempData[ORSPARouting.Models.DataConstants.ReattachDocumentsNoticeKey] = ORSPARouting.Models.DataConstants.ReattachDocumentsNotice;
                        SecureAccess.Utilities.Logging.LogException(Logging.LogType.Audit, "RequestController -> Create() : An error occurred when attempting to build a tblForm object from a Model.", ex);
                        Model.Project = FormsDB.tblProjects.Where(e => e.ID == Model.ProjectID).FirstOrDefault();
                        return View("New", Model);
                    }

                    // Save form to DB
                    Form.SubmitterUsername = Current.User.Username;
                    Form.Submitter = Current.User.ENumber;
                    Form.IsSubmitted = false; // only mark submitted when everything goes ok
                    Form.SubmittedDate = DateTime.Now;
                    FormsDB.tblForms.InsertOnSubmit(Form);
                    FormsDB.SubmitChanges();

                    #region Handle Existing Form (either submitting a rejected or unsubmitted form)
                    if (ExistingForm != null) {
                        // set Workflow from old form
                        Form.WorkflowInstanceID = ExistingForm.WorkflowInstanceID;

                        // set Documents from old form
                        foreach (tblDocument Doc in ExistingForm.tblDocuments.ToList()) {
                            Doc.tblForm = Form;
                        }

                        // delete old form data
                        FormsDB.tblFields.DeleteAllOnSubmit(ExistingForm.tblSections.SelectMany(e => e.tblFields));
                        FormsDB.tblSections.DeleteAllOnSubmit(ExistingForm.tblSections);
                        FormsDB.tblForms.DeleteOnSubmit(ExistingForm);
                    }
                    #endregion

                    // save changes before new document attachment
                    FormsDB.SubmitChanges();

                    #region Save Documents...
                    // save uploaded files
                    if (Model.Documents != null && Model.Documents.Files != null) {
                        List<tblDocument> DocumentsToSave = new List<tblDocument>();
                        foreach (FileBundle bundle in Model.Documents.Files) {
                            if (bundle.File != null && bundle.File.ContentLength > 0) {
                                // Save as unique path name  Form##_Filename.  ex.(Form13_ConflictofInterest.doc)
                                var fileName = String.Format("Form{0}_{1}", Form.ID, Path.GetFileName(bundle.File.FileName));
                                var path = Path.Combine(Server.MapPath("~/uploads"), fileName);
                                bundle.File.SaveAs(path);
                                tblDocument document = new tblDocument();
                                document.FormID = Form.ID;
                                document.FriendlyFilename = Path.GetFileName(bundle.File.FileName);
                                document.ActualFilename = fileName;
                                document.Description = bundle.Description;
                                document.Type = bundle.Type;
                                DocumentsToSave.Add(document);
                            }
                        }
                        if (DocumentsToSave.Count > 0) {
                            FormsDB.tblDocuments.InsertAllOnSubmit(DocumentsToSave);
                            FormsDB.SubmitChanges();
                        }
                    }
                    #endregion

                    #region Handle Workflow
                    // We're submitting request. If there was no existing form to get a workflow from or there was an existing form, but there is no WorkflowInstanceID, or the workflow exists and was rejected, create a workflow
                    if (ExistingForm == null 
                        || (ExistingForm != null && !Form.WorkflowInstanceID.HasValue) 
                        || (ExistingForm != null && Form.WorkflowInstanceID.HasValue && Workflows.GetWorkflow(Form.WorkflowInstanceID.Value).Rejected)  ) {
                            try {
                                #region Create Workflow...
                                // Create Workflow, save instance id to Form
                                tblProject Project = FormsDB.tblProjects.Where(e => e.ID == Model.ProjectID).SingleOrDefault();
                                List<WorkflowPersonnel> KeyPersonnel = Project.tblProjectPersonnels.Where(e => e.IsPrincipalInvestigator == false).Select(e => new WorkflowPersonnel() { ENumber = Banner.GetUser(e.Username).ENumber, OrganizationCode = e.DepartmentCollege }).ToList<WorkflowPersonnel>();
                                tblProjectPersonnel PIData = Project.tblProjectPersonnels.Where(e => e.IsPrincipalInvestigator).SingleOrDefault();
                                WorkflowPersonnel PI = new WorkflowPersonnel() { ENumber = Banner.GetUser(PIData.Username).ENumber, OrganizationCode = PIData.DepartmentCollege };
                                Form.WorkflowInstanceID = Workflows.CreateWorkflow(PI, KeyPersonnel);
                                FormsDB.SubmitChanges();
                                #endregion
                            }
                            catch (Exception ex) {
                                // on workflow create exception, set form as unsubmitted so it won't get stuck in limbo or lost.
                                Form.IsSubmitted = false;
                                FormsDB.SubmitChanges();
                                throw ex;
                            }
                    }
                    #endregion

                    // everything went ok, so mark form as submitted.
                    Form.IsSubmitted = true;
                    FormsDB.SubmitChanges();

                    SecureAccess.Utilities.Logging.Log(Logging.LogType.Audit, string.Format("User {0} successfully submitted a request. FormID: {1}; FormType: {2}", Current.User.Username, Form.ID,Form.FormType));
                }
                else {
                    TempData[ORSPARouting.Models.DataConstants.ReattachDocumentsNoticeKey] = ORSPARouting.Models.DataConstants.ReattachDocumentsNotice;
                    Model.Project = (new FormsDataContext()).tblProjects.Where(e => e.ID == Model.ProjectID).FirstOrDefault();
                    return View("New", Model);
                }
            }
            else {
                TempData[ORSPARouting.Models.DataConstants.ErrorKey] = ORSPARouting.Models.DataConstants.ModelStateInvalidError;
                TempData[ORSPARouting.Models.DataConstants.ReattachDocumentsNoticeKey] = ORSPARouting.Models.DataConstants.ReattachDocumentsNotice;
                Model.Project = (new FormsDataContext()).tblProjects.Where(e => e.ID == Model.ProjectID).FirstOrDefault();
                return View("New", Model);
            }

            return RedirectToAction("Status","Projects");
        }


        [DynamicFormAction]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Save(FormViewModel Model) {
            // builds new form data model from submitted form, keeps documents and workflow from old tblForm, and throws away the old values.
            FormsDataContext FormsDB = new FormsDataContext();
            tblForm ExistingForm = FormsDB.tblForms.Where(e => e.ID == Model.ID).SingleOrDefault();
            if (ExistingForm != null) Model.ReloadExistingDocuments(ExistingForm);

            if (ModelState.IsValid) {
                if (CustomRequestValidation(Model)) {
                    tblForm Form = null;
                        // Create form DB object from Model
                        try {
                            Form = new tblForm(Model);
                        }
                        catch (Exception ex) {
                            TempData[ORSPARouting.Models.DataConstants.ErrorKey] = "An error occurred when attempting to save the form. The values were not saved. The error has been logged.";
                            TempData[ORSPARouting.Models.DataConstants.ReattachDocumentsNoticeKey] = ORSPARouting.Models.DataConstants.ReattachDocumentsNotice;
                            SecureAccess.Utilities.Logging.LogException(Logging.LogType.Audit, "RequestController -> Save() : An error occurred when attempting to build a tblForm object from a Model.", ex);
                            Model.Project = FormsDB.tblProjects.Where(e => e.ID == Model.ProjectID).FirstOrDefault();
                            return View("Edit", Model);
                        }

                        // Save form data to DB
                        Form.SubmitterUsername = Current.User.Username;
                        Form.Submitter = Current.User.ENumber;
                        Form.IsSubmitted = false;
                        FormsDB.tblForms.InsertOnSubmit(Form);
                        FormsDB.SubmitChanges();

                        if (ExistingForm != null) {
                            // set Workflow from old form
                            Form.WorkflowInstanceID = ExistingForm.WorkflowInstanceID;
                            Form.IsSubmitted = ExistingForm.IsSubmitted;
                            Form.SubmittedDate = ExistingForm.SubmittedDate;

                            // set Documents from old form
                            foreach (tblDocument Doc in ExistingForm.tblDocuments.ToList()) {
                                Doc.tblForm = Form;
                            }

                            // delete old form data
                            FormsDB.tblFields.DeleteAllOnSubmit(ExistingForm.tblSections.SelectMany(e => e.tblFields));
                            FormsDB.tblSections.DeleteAllOnSubmit(ExistingForm.tblSections);
                            FormsDB.tblForms.DeleteOnSubmit(ExistingForm);
                        }

                        // save changes before new document attachment
                        FormsDB.SubmitChanges();

                        #region Save Documents from input...
                        if (Model.Documents != null && Model.Documents.Files != null) {
                            // save uploaded files
                            List<tblDocument> DocumentsToSave = new List<tblDocument>();
                            foreach (FileBundle bundle in Model.Documents.Files) {
                                if (bundle.File != null && bundle.File.ContentLength > 0) {
                                    // Save as unique path name  Form##_Filename.  ex.(Form13_ConflictofInterest.doc)
                                    var fileName = String.Format("Attachment_{0}_{1}", Path.GetFileName(bundle.File.FileName), DateTime.Now.ToString("yyyyMMddHmmss"));
                                    var path = Path.Combine(Server.MapPath("~/uploads"), fileName);
                                    bundle.File.SaveAs(path);
                                    tblDocument document = new tblDocument();
                                    document.FormID = Form.ID;
                                    document.FriendlyFilename = Path.GetFileName(bundle.File.FileName);
                                    document.ActualFilename = fileName;
                                    document.Description = bundle.Description;
                                    document.Type = bundle.Type;
                                    DocumentsToSave.Add(document);
                                }
                            }
                            if (DocumentsToSave.Count > 0) {
                                FormsDB.tblDocuments.InsertAllOnSubmit(DocumentsToSave);
                                FormsDB.SubmitChanges();
                            }
                        }
                        #endregion

                        // Don't Create Workflow

                        // log
                        SecureAccess.Utilities.Logging.Log(Logging.LogType.Audit, string.Format("User {0} successfully saved a request. FormID: {1}; FormType: {2}", Current.User.Username, Form.ID, Form.FormType));
                        this.ShowPageMessage(String.Format("Request successfully saved: {0}",DateTime.Now.ToLongTimeString()));
                        return RedirectToAction("Edit", new { @ID=Form.ID });
                        
                } // end custom validation IF block
                else {
                    TempData[ORSPARouting.Models.DataConstants.ReattachDocumentsNoticeKey] = ORSPARouting.Models.DataConstants.ReattachDocumentsNotice;
                    Model.Project = (new FormsDataContext()).tblProjects.Where(e => e.ID == Model.ProjectID).FirstOrDefault();
                    return View("Edit", Model);
                }
            }
            else {
                TempData[ORSPARouting.Models.DataConstants.ErrorKey] = ORSPARouting.Models.DataConstants.ModelStateInvalidError;
                TempData[ORSPARouting.Models.DataConstants.ReattachDocumentsNoticeKey] = ORSPARouting.Models.DataConstants.ReattachDocumentsNotice;
                Model.Project = FormsDB.tblProjects.Where(e => e.ID == Model.ProjectID).FirstOrDefault();
                return View("Edit", Model);
            }

            // all paths above should return a value
        }


        /// <summary>
        /// Additional custom validation for requests that are not easily done through data validation attributes.
        /// </summary>
        /// <param name="Model"></param>
        /// <returns></returns>
        private bool CustomRequestValidation(FormViewModel Model) {
            
            // validate required document attachments
            if (Model.FormType == (int)DataConstants.FormType.Proposal) {
                // Make sure proposal/scope of work/abstract  and ORSPA budget worksheet is attached
                if ((Model.Documents == null || Model.Documents.ExistingDocuments.Where(e => e.Type == DataConstants.DocumentType.ProposalScopeOfWorkAbstract.ToString()).Count() <= 0)
                    && Model.Documents.Files.Where(e=>e.Type == DataConstants.DocumentType.ProposalScopeOfWorkAbstract.ToString()).Count() <= 0) {
                        this.ShowPageError("Missing attachment type: proposal/scope of work/abstract");
                        return false;
                }
                if ((Model.Documents == null || Model.Documents.ExistingDocuments.Where(e => e.Type == DataConstants.DocumentType.ORSPABudgetWorksheet.ToString()).Count() <= 0)
                    && Model.Documents.Files.Where(e => e.Type == DataConstants.DocumentType.ORSPABudgetWorksheet.ToString()).Count() <= 0) {
                        this.ShowPageError("Missing attachment type: ORSPA budget worksheet");
                        return false;
                }
            }
            else if (Model.FormType == (int)DataConstants.FormType.Contract || Model.FormType == (int)DataConstants.FormType.ContractFromPreviousProposal) {
                // Make sure proposal/scope of work/abstract is attached
                if ((Model.Documents == null || Model.Documents.ExistingDocuments.Where(e => e.Type == DataConstants.DocumentType.ProposalScopeOfWorkAbstract.ToString()).Count() <= 0)
                    && Model.Documents.Files.Where(e => e.Type == DataConstants.DocumentType.ProposalScopeOfWorkAbstract.ToString()).Count() <= 0) {
                    this.ShowPageError("Missing attachment type: proposal/scope of work/abstract");
                    return false;
                }
            }

            // validate attachment for Indirect Cost Reduction/Waiver 
            if (Model.FormType == (int)DataConstants.FormType.Proposal || Model.FormType == (int)DataConstants.FormType.Contract || Model.FormType == (int)DataConstants.FormType.ContractFromPreviousProposal) {
                // validate attachment for Indirect Cost Reduction/Waiver if "yes" is selected
                if (Model.SectionE.IndirectCostReductionWaiverChoice == ((int)DataConstants.YesNoType.Yes).ToString()) {
                    if ((Model.Documents == null || Model.Documents.ExistingDocuments.Where(e => e.Type == DataConstants.DocumentType.IndirectCostWaiverForm.ToString()).Count() <= 0)
                        && Model.Documents.Files.Where(e => e.Type == DataConstants.DocumentType.IndirectCostWaiverForm.ToString()).Count() <= 0) {
                        this.ShowPageError("Missing attachment type: indirect cost waiver request form");
                        return false;
                    }
                }
                if (Model.SectionE.GATuitionWaiverChoice == ((int)DataConstants.YesNoType.Yes).ToString()) {
                    if ((Model.Documents == null || Model.Documents.ExistingDocuments.Where(e => e.Type == DataConstants.DocumentType.GraduateStudiesLetterOfApproval.ToString()).Count() <= 0)
                        && Model.Documents.Files.Where(e => e.Type == DataConstants.DocumentType.GraduateStudiesLetterOfApproval.ToString()).Count() <= 0) {
                        this.ShowPageError("Missing attachment type: School of Graduate Studies letter of approval");
                        return false;
                    }
                }
            }

            // validate document comments
            if (Model.Documents != null && Model.Documents.Files != null) {
                foreach (FileBundle bundle in Model.Documents.Files) {
                    if (bundle.File != null && bundle.File.ContentLength > 0) {
                        if (string.IsNullOrEmpty(bundle.Description)) {
                            this.ShowPageError("Attached documents must have a description. Please re-attach your documents and provide a comment for each.");
                            return false;
                        }
                    }
                }
            }

            return true; // assume if we didn't bail, everything is valid
        }


    }
}
