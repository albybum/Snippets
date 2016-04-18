using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CPS_Navigator.Models;

namespace CPS_Navigator.Controllers
{

    [Authorize(Roles = "Admin,Viewer")]
    public class FormsController : ControllerBase
    {
        //
        // GET: /Forms/
        InputFormEntities _Forms = new Models.InputFormEntities();
        ETSU_UsersEntities _Users = new Models.ETSU_UsersEntities();
        WorkflowEntities _Workflows = new Models.WorkflowEntities();

        public ActionResult Index()
        {
            List<FormTemplate> Templates = _Forms.FormTemplates.Where(e=>e.ProgramID == 5).ToList();
            ViewBag.Templates = Templates;
            List<SelectListItem> TemplateOptions = new List<SelectListItem>() { new SelectListItem() { Text = "- Select a Template -", Value="-1" } };
            TemplateOptions.AddRange(Templates.Select(e => new SelectListItem() { Value = e.FormTemplateID.ToString(), Text = e.FormName }).ToList());
            ViewBag.TemplateOptions = TemplateOptions;
            return View();
        }


        public ActionResult InstancesByTemplate(int id) {
            FormTemplate Template = _Forms.FormTemplates.Where(e=>e.FormTemplateID == id).SingleOrDefault();
            List<FormInstance> Instances = _Forms.FormInstances.Where(e => e.FormTemplateID == id).OrderByDescending(e=>e.CreationTime).ToList();
            ViewBag.Template = Template;
            ViewBag.Instances = Instances;
            return View();
        }


        public ActionResult FormInstanceData(int id) {
            FormInstance Instance = _Forms.FormInstances.Where(e => e.FormInstanceID == id).SingleOrDefault();
            List<FieldInstance> Fields = _Forms.FieldInstances.Where(e => e.FormInstanceID == id).OrderBy(e => e.FieldName).ToList();
            ViewBag.Instance = Instance;
            ViewBag.Fields = Fields;
            return View();
        }

        public ActionResult Workflow(int id) {
            tblWorkFlowInstance Workflow = _Workflows.tblWorkFlowInstances.Where(e => e.WorkflowInstanceID == id).SingleOrDefault();
            List<tblStepInstanceSequence> StepInstanceSequences = _Workflows.tblStepInstanceSequences.Where(e => e.WorkflowInstanceID == id).OrderBy(e=>e.StepNumber).ToList();
            List<int> InstanceIDs = StepInstanceSequences.Select(e=>e.StepInstanceID).ToList();
            List<tblStepInstance> StepInstances = _Workflows.tblStepInstances.Where(e => InstanceIDs.Contains(e.StepInstanceID)).ToList();

            ViewBag.Workflow = Workflow;
            ViewBag.StepInstanceSequences = StepInstanceSequences;
            ViewBag.StepInstances = StepInstances;
            
            return View();
        }

        public ActionResult SearchForms(int? TemplateID = null, string HasWorkflow = null, string FieldName = null, string FieldValue = null, int? FormInstanceID = null, int? WorkflowInstanceID = null, string Username = null) {
            IQueryable<FormInstance> Instances = _Forms.FormInstances.AsQueryable();

            if(TemplateID.HasValue && TemplateID.Value != -1)
                Instances = Instances.Where(e => e.FormTemplateID == TemplateID).AsQueryable();

            if (!string.IsNullOrEmpty(HasWorkflow)) {
                if(HasWorkflow.Equals("Yes",StringComparison.CurrentCultureIgnoreCase)) {
                    Instances = Instances.Where(e => e.WorkflowInstanceID.HasValue == true).AsQueryable();
                }
                if (HasWorkflow.Equals("No", StringComparison.CurrentCultureIgnoreCase)) {
                    Instances = Instances.Where(e => e.WorkflowInstanceID.HasValue == false).AsQueryable();
                }
            }

            if (FormInstanceID.HasValue)
                Instances = Instances.Where(e => e.FormInstanceID == FormInstanceID.Value).AsQueryable();

            if (WorkflowInstanceID.HasValue)
                Instances = Instances.Where(e => e.WorkflowInstanceID == WorkflowInstanceID.Value).AsQueryable();

            if (!string.IsNullOrEmpty(Username)) {
                tblUserLanID usr = _Users.tblUserLanIDs.Where(e => e.LName.ToUpper() == Username.ToUpper()).SingleOrDefault();
                if (usr != null) {
                    Instances = Instances.Where(e => e.CreatedByUserID == usr.UserIdx).AsQueryable();
                }
            }

            // least efficient, save for last
            if (!string.IsNullOrEmpty(FieldName))
                Instances = Instances.Where(e => e.FieldInstances.Where(f => f.FieldName.ToUpper().Contains(FieldName.ToUpper())).Count() > 0).AsQueryable();

            if (!string.IsNullOrEmpty(FieldValue))
                Instances = Instances.ToList().Where(e => e.FieldInstances.Where(f => f.FieldValue != null && f.FieldValue.ToUpper().Contains(FieldValue.ToUpper())).Count() > 0).AsQueryable();

            ViewBag.Instances = Instances.OrderByDescending(e => e.CreationTime).ToList();
            return View();
        }


    }
}
