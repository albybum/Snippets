using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SecureAccess.Utilities;
using Orientation.Classes.SQL;
using System.Data.OracleClient;
using System.Data;
using System.Text;
using Orientation.Classes;
using Orientation.Models.BannerDTOs;

namespace Orientation.Models {
    public class BannerDB {
        #region Members...
        private static SqlResource _QUERIES = null; // static SQL Resource, shared across all instances of BannerData.
        private static Database _DB = null; // static, shared across all instances of BannerData.
        #endregion

        #region Constants...
        // DB Names (connection string name)
        private const string DB_BANNER = "Banner";
        // Files
        private const string FILE_QUERIES = "~/App_Data/BannerQueries.xml";
        // Parameter Keys
        private const string PARAM_PIDM = "PIDM";
        private const string PARAM_ENUMBER = "ENUMBER";
        private const string PARAM_TERM = "TERM";
        private const string PARAM_FIRST_NAME = "FIRST_NAME";
        private const string PARAM_LAST_NAME = "LAST_NAME";
        private const string PARAM_TEST_CODE = "TEST_CODE";
        private const string PARAM_MIN_SCORE = "MIN_SCORE";
        private const string PARAM_MAX_SCORE = "MAX_SCORE";
        private const string PARAM_ENUMBERS = "ENUMBERS";
        private const string PARAM_HOLD_CODE = "HOLD_CODE";
        private const string PARAM_LEVEL = "STUDENT_LEVEL";
        private const string PARAM_HOLD_TYPE = "HOLDTYPE";
        private const string PARAM_PROGRAM_CODE = "PROGRAMCODE";
        private const string PARAM_MIN_GPA = "GPA";
        private const string PARAM_ORSN = "ORSN";
        private const string PARAM_PAID_INDICATOR = "PAID_IND";
        private const string PARAM_ENUMLIST = "ENUMLIST";
        // SQL Keys
        private const string SQL_GET_FINAL_TERM_END_DATE = "GET_FINAL_TERM_END_DATE";
        private const string SQL_IS_CURRENT_STUDENT = "IS_CURRENT_STUDENT";
        private const string SQL_GET_FIRST_TIME_FRESHMEN = "GET_FIRST_TIME_FRESHMEN";
        private const string SQL_GET_STUDENT_IDS_BY_NAME = "GET_STUDENT_IDS_BY_NAME";
        private const string SQL_GET_TRANSFER_STUDENTS = "GET_TRANSFER_STUDENTS";
        private const string SQL_GET_STUDENTS_BY_ENUMBER_LIST = "GET_STUDENTS_BY_ENUMBER_LIST";
        private const string SQL_GET_ADULT_STUDENTS = "GET_ADULT_STUDENTS";
        private const string SQL_GET_UNDER23_STUDENTS = "GET_UNDER23_STUDENTS";
        private const string SQL_GET_BY_GPA_AND_TEST_SCORE = "GET_STUDENTS_BY_GPA_AND_TEST_SCORE";
        private const string SQL_IS_ADMITTED = "IS_ADMITTED";
        private const string SQL_GET_BY_TEST_SCORE = "GET_STUDENTS_BY_TEST_SCORE";
        private const string SQL_GET_ONLINE_ELIGIBLE = "GET_ONLINE_ELIGIBLE";

        private const string SQL_GET_DUAL_ADMIT_ONLINE_ONLY = "GET_DUAL_ADMIT_ONLINE";

        private const string SQL_GET_HOLDS = "GET_HOLDS";
        private const string SQL_GET_HOLDS_EFFICIENTLY = "GET_HOLDS_MULTIPLE";
        private const string SQL_RELEASE_HOLD = "RELEASE_HOLD";
        private const string SQL_RELEASE_HOLD_BULK = "RELEASE_HOLD_BULK";
        private const string SQL_GET_STUDENT_TEST_SCORES = "GET_STUDENT_TEST_SCORES";
        private const string SQL_GET_STUDENT_HIGH_SCHOOLS = "GET_HIGH_SCHOOLS";
        private const string SQL_GET_STUDENT_HIGH_SCHOOL_DEFICIENCIES = "GET_HIGH_SCHOOL_DEFICIENCIES";
        private const string SQL_GET_LEARNING_SUPPORT_REQUIREMENTS = "GET_LEARNING_SUPPORT_REQUIREMENTS";
        private const string SQL_GET_TRANSFER_ACADEMIC_HISTORY = "GET_TRANSFER_ACADEMIC_HISTORY";
        private const string SQL_GET_STUDENT_INFO = "GET_STUDENT_INFO";
        private const string SQL_GET_STUDENT_APP_INFO = "GET_STUDENT_APP_INFO";
        private const string SQL_GET_ADDRESSES = "GET_ADDRESSES";
        private const string SQL_GET_PHONE_NUMBERS = "GET_PHONE_NUMBERS";
        private const string SQL_GET_EMAIL_ADDRESSES = "GET_EMAIL_ADDRESSES";
        private const string SQL_GET_EMAIL_ADDRESSES_BULK = "GET_EMAIL_ADDRESSES_BULK";
        private const string SQL_GET_PROGRAM_CODES = "GET_PROGRAM_CODES";
        private const string SQL_GET_STUDENT_STATISTICS = "GET_STUDENT_STATISTICS";
        private const string SQL_GET_MAIL_MERGE = "GET_MAIL_MERGE_REPORT";
        private const string SQL_GET_INVITED_PAID = "GET_INVITED_PAID";
        private const string SQL_GET_EVENT_SUMMARY = "GET_EVENT_SUMMARY";
        private const string SQL_GET_SUMMARY_DEVELOPMENTAL_STUDIES = "GET_SUMMARY_DEVELOPMENTAL_STUDIES";
        private const string SQL_GET_SUMMARY_OTHER_COUNTS = "GET_SUMMARY_OTHER_COUNTS";
        private const string SQL_GET_SUMMARY_TOTALS = "GET_SUMMARY_TOTALS";
        private const string SQL_GET_EVENT_ANALYSIS = "GET_EVENT_ANALYSIS";
        private const string SQL_GET_HEADCOUNTS = "GET_HEADCOUNTS";
        private const string SQL_GET_IMMUNIZATIONS_REPORT = "GET_IMMUNIZATIONS_REPORT";
        private const string SQL_HAS_SELECTIVE_SERVICE_HOLD = "CHECKLIST_HAS_SELECTIVE_SERVICE_HOLD";
        private const string SQL_MUST_VERIFY_CITIZENSHIP = "MUST_VERIFY_CITIZENSHIP";
        private const string SQL_GET_STUDENT_REQUIREMENTS_REQUESTED = "GET_STUDENT_REQUIREMENTS_REQUESTED";
        private const string SQL_GET_FIN_AID_CHECKLIST_REQUIREMENTS_REQUESTED = "GET_FIN_AID_CHECKLIST_REQUIREMENTS_REQUESTED";
        private const string SQL_GET_IMMUNIZATION_CHECKLIST = "GET_IMMUNIZATION_CHECKLIST";
        private const string SQL_GET_FAFSA_AID_YEAR = "GET_FAFSA_AID_YEAR";
        private const string SQL_UPDATE_SGBSTDN_ORSN_CODE = "UPDATE_SGBSTDN_ORSN";
        private const string SQL_INSERT_IDCARD_FLAG = "INSERT_SYRORIE_IDCARD_ROW";
        #endregion

        #region Constructors
        public BannerDB() {
            // only load sql from xml when sqlresource is null 
            if (_QUERIES == null)
                _QUERIES = new SqlResource(HttpContext.Current.Server.MapPath(FILE_QUERIES));
            // only initialize the Banner DB when it is null
            if (_DB == null)
                _DB = new Database(DB_BANNER);
        }

        #endregion


        #region Private Methods

        // Get a strictly-typed list of an abritrary data model type for a given script name and list of invitations.
        private List<T> GetData<T>(string ScriptName, List<tblInvitation> Invitations) where T : new()
        {
            List<T> retVal = new List<T>();
            List<string> ENumbers = Invitations.Select(e => e.ENumber).Distinct().ToList();

            // param exceeds 4000 characters, so chunk up the enumbers into buckets and process each bucket.
            int loops = (int)Math.Ceiling((double)((double)ENumbers.Count / (double)400));
            for (int i = 0; i < loops; i++)
            {
                string enumbersToBindLooped = String.Join(",", ENumbers.Skip(i * 400).Take(400).ToList().ToArray());
                if (!string.IsNullOrEmpty(enumbersToBindLooped))
                {
                    if (!enumbersToBindLooped.EndsWith(","))
                        enumbersToBindLooped += ",";
                    List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
                    paramList.Add(new OracleParameter(PARAM_ENUMBERS, enumbersToBindLooped));
                    List<T> summary = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(ScriptName).Sql, paramList).AsEnumerable().Select(e => (T)Activator.CreateInstance(typeof(T), e)).ToList();
                    retVal.AddRange(summary);
                }
            }
            return retVal;
        }

        // Get a strictly-typed list of an abritrary data model type for a given script name and list of invitations. Optionally filter by program code
        private List<T> GetData<T>(string ScriptName, List<tblInvitation> Invitations, string ProgramCode = null) where T : new()
        {
            List<T> retVal = new List<T>();
            List<string> ENumbers = Invitations.Select(e => e.ENumber).Distinct().ToList();

            // param exceeds 4000 characters, so chunk up the enumbers into buckets and process each bucket.
            int loops = (int)Math.Ceiling((double)((double)ENumbers.Count / (double)400));
            for (int i = 0; i < loops; i++)
            {
                string enumbersToBindLooped = String.Join(",", ENumbers.Skip(i * 400).Take(400).ToList().ToArray());
                if (!string.IsNullOrEmpty(enumbersToBindLooped))
                {
                    if (!enumbersToBindLooped.EndsWith(","))
                        enumbersToBindLooped += ",";
                    List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
                    paramList.Add(new OracleParameter(PARAM_ENUMBERS, enumbersToBindLooped));
                    paramList.Add(new OracleParameter(PARAM_PROGRAM_CODE, ProgramCode != null ? (object)ProgramCode : DBNull.Value));
                    List<T> summary = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(ScriptName).Sql, paramList).AsEnumerable().Select(e => (T)Activator.CreateInstance(typeof(T), e)).ToList();
                    retVal.AddRange(summary);
                }
            }
            return retVal;
        }

        private List<T> GetData<T>(string ScriptName, List<tblInvitation> Invitations, string Term, string ProgramCode = null) where T : new()
        {
            List<T> retVal = new List<T>();
            List<string> ENumbers = Invitations.Select(e => e.ENumber).Distinct().ToList();

            // param exceeds 4000 characters, so chunk up the enumbers into buckets and process each bucket.
            int loops = (int)Math.Ceiling((double)((double)ENumbers.Count / (double)400));
            for (int i = 0; i < loops; i++)
            {
                string enumbersToBindLooped = String.Join(",", ENumbers.Skip(i * 400).Take(400).ToList().ToArray());
                if (!string.IsNullOrEmpty(enumbersToBindLooped))
                {
                    if (!enumbersToBindLooped.EndsWith(","))
                        enumbersToBindLooped += ",";
                    List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
                    paramList.Add(new OracleParameter(PARAM_ENUMBERS, enumbersToBindLooped));
                    paramList.Add(new OracleParameter(PARAM_TERM, Term));
                    paramList.Add(new OracleParameter(PARAM_PROGRAM_CODE, ProgramCode != null ? (object)ProgramCode : DBNull.Value));
                    List<T> summary = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(ScriptName).Sql, paramList).AsEnumerable().Select(e => (T)Activator.CreateInstance(typeof(T), e)).ToList();
                    retVal.AddRange(summary);
                }
            }
            return retVal;
        }

        #endregion



        #region Public Methods

        // simple query to test database connectivity (select 1 from dual)
        public bool Ping() {
            try {
                Query testQuery = _QUERIES.GetQueryByName("TestQuery");
                string test = _DB.ExecuteSQLCommandAsScalar(testQuery.Sql);
                return test.Equals("1", StringComparison.CurrentCultureIgnoreCase);
            }
            catch (Exception ex) {
                SecureAccess.Utilities.Logging.LogException(Logging.LogType.Audit, "An exception occurred when trying to ping Banner.", ex);
                return false;
            }
        }

        public List<Orientation.Models.BannerDTOs.EmailAddress> GetEmailsBulk(List<tblInvitation> Invitations)
        {
            return this.GetData<Orientation.Models.BannerDTOs.EmailAddress>(SQL_GET_EMAIL_ADDRESSES_BULK, Invitations);
        }

        public List<SummaryCount> GetDevelopmentalStudiesSummaryCount(List<tblInvitation> Invitations, string Term, string ProgramCode = null)
        {
            return this.GetData<SummaryCount>(SQL_GET_SUMMARY_DEVELOPMENTAL_STUDIES, Invitations, Term, ProgramCode);
        }

        public List<SummaryCount> GetOtherSummaryCount(List<tblInvitation> Invitations, string Term, string ProgramCode = null)
        {
            return this.GetData<SummaryCount>(SQL_GET_SUMMARY_OTHER_COUNTS, Invitations, Term, ProgramCode);
        }

        public List<SummaryCount> GetTotalSummaryCount(List<tblInvitation> Invitations, string Term, string ProgramCode = null)
        {
            return this.GetData<SummaryCount>(SQL_GET_SUMMARY_TOTALS, Invitations, Term, ProgramCode);
        }

        public List<EventAnalysis> GetEventAnalysis(List<tblInvitation> Invitations, string Term, string ProgramCode = null)
        {
            return this.GetData<EventAnalysis>(SQL_GET_EVENT_ANALYSIS, Invitations, Term, ProgramCode);
        }

        public List<HeadCount> GetHeadcounts(List<tblInvitation> Invitations, string Term, string ProgramCode = null)
        {
            return this.GetData<HeadCount>(SQL_GET_HEADCOUNTS, Invitations, Term, ProgramCode);
        }


        /// <summary>
        /// Gets the date associated with the final month of the final term for the given student by ENumber
        /// </summary>
        /// <param name="ENumber"></param>
        /// <returns></returns>
        public string GetFinalTermEndDate(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsScalar(_QUERIES.GetQueryByName(SQL_GET_FINAL_TERM_END_DATE).Sql, param);
        }

        /// <summary>
        /// Determines if the student is admitted for the given term
        /// </summary>
        /// <param name="ENumber"></param>
        /// <param name="Term"></param>
        /// <returns></returns>
        public bool IsAdmitted(string ENumber, string Term) {
            List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
            paramList.Add(new OracleParameter(PARAM_ENUMBER, ENumber));
            paramList.Add(new OracleParameter(PARAM_TERM, Term));
            return _DB.ExecuteSQLCommandAsScalar(_QUERIES.GetQueryByName(SQL_IS_ADMITTED).Sql, paramList) == "Y";
        }

        /// <summary>
        /// Determines if the students for the given ENumber is a current student
        /// </summary>
        /// <param name="ENumber"></param>
        /// <returns>True if the student is a current student</returns>
        public bool IsCurrentStudent(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsScalar(_QUERIES.GetQueryByName(SQL_IS_CURRENT_STUDENT).Sql, param) == "Y";
        }

        public List<StudentSearchResult> GetFirstTimeFreshmen(string Term) {
            OracleParameter param = new OracleParameter(PARAM_TERM, Term);
            OrientationDB _ODB = new OrientationDB();
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_FIRST_TIME_FRESHMEN).Sql, param).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }

        public List<StudentSearchResult> GetTransferStudents(string Term) {
            OracleParameter param = new OracleParameter(PARAM_TERM, Term);
            OrientationDB _ODB = new OrientationDB();
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_TRANSFER_STUDENTS).Sql, param).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }

        public List<StudentSearchResult> GetWhitelist(int EventId)
        {
            OrientationDB _ODB = new OrientationDB();
            var Event = _ODB.tblEvents.Where(e => e.ID == EventId).SingleOrDefault();
            if (Event == null || String.IsNullOrWhiteSpace(Event.Whitelist))
            {
                return new List<StudentSearchResult>();
            }
            else
            {
                OracleParameter param = new OracleParameter(PARAM_ENUMLIST, String.Format(",{0},", Event.Whitelist.Replace(' ', ',')));
                List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_STUDENTS_BY_ENUMBER_LIST).Sql, param).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
                retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
                return retVal;
            }
        }

        public List<StudentSearchResult> GetAdultStudents(string Term) {
            OracleParameter param = new OracleParameter(PARAM_TERM, Term);
            OrientationDB _ODB = new OrientationDB();
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_ADULT_STUDENTS).Sql, param).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }

        public List<StudentSearchResult> GetUnder23Students(string Term) {
            OracleParameter param = new OracleParameter(PARAM_TERM, Term);
            OrientationDB _ODB = new OrientationDB();
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_UNDER23_STUDENTS).Sql, param).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }

        public List<StudentSearchResult> GetStudentIDsByName(string FirstName, string LastName) {
            List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
            OrientationDB _ODB = new OrientationDB();
            paramList.Add(new OracleParameter(PARAM_FIRST_NAME, FirstName));
            paramList.Add(new OracleParameter(PARAM_LAST_NAME, LastName));
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_STUDENT_IDS_BY_NAME).Sql, paramList).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = FirstName, LastName = LastName, ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }

        public List<StudentSearchResult> GetStudentsByTestScore(string Term, string TestCode, string MinScore, string MaxScore) {
            List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
            OrientationDB _ODB = new OrientationDB();
            paramList.Add(new OracleParameter(PARAM_TERM, Term));
            paramList.Add(new OracleParameter(PARAM_TEST_CODE, TestCode));
            paramList.Add(new OracleParameter(PARAM_MIN_SCORE, MinScore));
            paramList.Add(new OracleParameter(PARAM_MAX_SCORE, MaxScore));
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_BY_TEST_SCORE).Sql, paramList).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }

        public List<StudentSearchResult> GetStudentsByGPAAndTestScore(string Term, string TestCode, string MinScore, string MaxScore, string MinGPA) {
            List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
            OrientationDB _ODB = new OrientationDB();
            paramList.Add(new OracleParameter(PARAM_TERM, Term));
            paramList.Add(new OracleParameter(PARAM_TEST_CODE, TestCode));
            paramList.Add(new OracleParameter(PARAM_MIN_SCORE, MinScore));
            paramList.Add(new OracleParameter(PARAM_MAX_SCORE, MaxScore));
            paramList.Add(new OracleParameter(PARAM_MIN_GPA, MinGPA));
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_BY_GPA_AND_TEST_SCORE).Sql, paramList).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }
        
        public List<StudentSearchResult> GetOnlineEligibleStudents(string Term) {
            OracleParameter param = new OracleParameter(PARAM_TERM, Term);
            OrientationDB _ODB = new OrientationDB();
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_ONLINE_ELIGIBLE).Sql, param).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }

        public List<StudentSearchResult> GetDualAdmitOnlineOnly(string Term)
        {
            OracleParameter param = new OracleParameter(PARAM_TERM, Term);
            OrientationDB _ODB = new OrientationDB();
            List<StudentSearchResult> retVal = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_DUAL_ADMIT_ONLINE_ONLY).Sql, param).AsEnumerable().Select(e => new StudentSearchResult() { FirstName = e.Field<string>(2), LastName = e.Field<string>(3), ENumber = e.Field<string>(0), BirthDate = e.Field<DateTime?>(1) }).ToList();
            retVal.ForEach(e => e.Invitations = _ODB.tblInvitations.Where(f => f.ENumber == e.ENumber).ToList());
            return retVal;
        }

        public List<StudentHold> GetHolds(List<string> ENumbers, string HoldType = "ALL") {
            List<StudentHold> retVal = new List<StudentHold>();

            string enumbersToBind = String.Join(",", ENumbers.ToArray());
            var holdType = string.IsNullOrEmpty(HoldType) ? "ALL" : HoldType;

            // if comma delimieted list of enumbers is less than the 4000 character hard parameter BIND limit, use most efficient method
            if (enumbersToBind.Length <= 4000) {
                List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
                paramList.Add(new OracleParameter(PARAM_ENUMBERS, enumbersToBind));
                paramList.Add(new OracleParameter(PARAM_HOLD_TYPE, holdType));
                List<StudentHold> holds = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_HOLDS_EFFICIENTLY).Sql, paramList).AsEnumerable().Select(e => new StudentHold() { FirstName = e.Field<string>(2), LastName = e.Field<string>(4), MiddleName = e.Field<string>(3), ENumber = e.Field<string>(1), PIDM = e.Field<string>(0), Code = e.Field<string>(5), From = e.Field<string>(6), To = e.Field<string>(7), Reason = e.Field<string>(8) }).ToList();
                retVal.AddRange(holds);
            }
            else {
                // param exceeds 4000 characters, so chunk up the enumbers into buckets and process each bucket.
                int loops = (int)Math.Ceiling((double)(ENumbers.Count / 400));
                for (int i = 0; i <= loops; i++) {
                    string enumbersToBindLooped = String.Join(",", ENumbers.Skip(i * 400).Take(400).ToList().ToArray());
                    if (!string.IsNullOrEmpty(enumbersToBindLooped)) {
                        if (!enumbersToBindLooped.EndsWith(","))
                            enumbersToBindLooped += ",";
                        List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
                        paramList.Add(new OracleParameter(PARAM_ENUMBERS, enumbersToBindLooped));
                        paramList.Add(new OracleParameter(PARAM_HOLD_TYPE, holdType));
                        List<StudentHold> holds = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_HOLDS_EFFICIENTLY).Sql, paramList).AsEnumerable().Select(e => new StudentHold() { FirstName = e.Field<string>(2), LastName = e.Field<string>(4), MiddleName = e.Field<string>(3), ENumber = e.Field<string>(1), PIDM = e.Field<string>(0), Code = e.Field<string>(5), From = e.Field<string>(6), To = e.Field<string>(7), Reason = e.Field<string>(8) }).ToList();
                        retVal.AddRange(holds);
                    }
                }
            }
            return retVal;
        }

        public int ReleaseHold(string ENumber, string HoldCode) {
            List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
            OrientationDB _ODB = new OrientationDB();
            paramList.Add(new OracleParameter(PARAM_ENUMBER, ENumber));
            paramList.Add(new OracleParameter(PARAM_HOLD_CODE, HoldCode));
            int retVal = _DB.ExecuteSQLCommand(_QUERIES.GetQueryByName(SQL_RELEASE_HOLD).Sql, paramList);
            return retVal;
        }

        public int ReleaseHoldBulk(List<string> ENumbers, string HoldCode) {
            int retVal = -1;

            // in case param exceeds 4000 characters, so chunk up the enumbers into buckets and process each bucket.
            int loops = (int)Math.Ceiling((double)(ENumbers.Count / 400));
            for (int i = 0; i <= loops; i++) {
                string enumbersToBindLooped = String.Join(",", ENumbers.Skip(i * 400).Take(400).ToList().ToArray());
                if (!string.IsNullOrEmpty(enumbersToBindLooped)) {
                    if (!enumbersToBindLooped.EndsWith(","))
                        enumbersToBindLooped += ",";
                    List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
                    paramList.Add(new OracleParameter(PARAM_ENUMBERS, enumbersToBindLooped));
                    paramList.Add(new OracleParameter(PARAM_HOLD_CODE, HoldCode));
                    retVal += _DB.ExecuteSQLCommand(_QUERIES.GetQueryByName(SQL_RELEASE_HOLD_BULK).Sql, paramList);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Updates the Orientation Code on SGBSTDN for student.
        /// </summary>
        /// <param name="ENumber"></param>
        /// <param name="ORSN"></param>
        /// <returns></returns>
        public int UpdateSGBSTDN_ORSN_Code(string ENumber, string ORSN) {
            List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
            OrientationDB _ODB = new OrientationDB();
            paramList.Add(new OracleParameter(PARAM_ENUMBER, ENumber));
            paramList.Add(new OracleParameter(PARAM_ORSN, ORSN));
            int retVal = _DB.ExecuteSQLCommand(_QUERIES.GetQueryByName(SQL_UPDATE_SGBSTDN_ORSN_CODE).Sql, paramList);
            return retVal;
        }

   
        /// <summary>
        /// Inserts a row into the old SYRORIE table so the ID Card Data Extract can pickup the students
        /// </summary>
        /// <param name="Pidm"></param>
        /// <param name="TERM"></param>
        /// <param name="PAID_IND"></param>
        /// <returns></returns>
        public int INSERT_IDCARD_FLAG(string Pidm, string TERM, string PAID_IND) {
            List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
            OrientationDB _ODB = new OrientationDB();
            paramList.Add(new OracleParameter(PARAM_PIDM, Pidm));
            paramList.Add(new OracleParameter(PARAM_TERM, TERM));
            paramList.Add(new OracleParameter(PARAM_PAID_INDICATOR, PAID_IND));
            int retVal = _DB.ExecuteSQLCommand(_QUERIES.GetQueryByName(SQL_INSERT_IDCARD_FLAG).Sql, paramList);
            return retVal;
        }

        public DataTable GetHighSchools(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_STUDENT_HIGH_SCHOOLS).Sql, param);
        }

        public DataTable GetTestScores(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_STUDENT_TEST_SCORES).Sql, param);
        }

        public DataTable GetHighSchoolDeficiencies(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_STUDENT_HIGH_SCHOOL_DEFICIENCIES).Sql, param);
        }

        public DataTable GetLearningSupportRequirements(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_LEARNING_SUPPORT_REQUIREMENTS).Sql, param);
        }

        public DataTable GetTransferHistory(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_TRANSFER_ACADEMIC_HISTORY).Sql, param);
        }

        public DataRow GetStudentInfo(string ENumber, string Level = "UG") {
            List<System.Data.Common.DbParameter> paramList = new List<System.Data.Common.DbParameter>();
            paramList.Add(new OracleParameter(PARAM_ENUMBER, ENumber));
            paramList.Add(new OracleParameter(PARAM_LEVEL, Level));
            DataTable results = _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_STUDENT_INFO).Sql, paramList);
            if (results.Rows.Count > 0)
                return results.Rows[0];
            else
                return null;
        }

        public DataTable GetApplicationInformation(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_STUDENT_APP_INFO).Sql, param);
        }

        public DataTable GetAddresses(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_ADDRESSES).Sql, param);
        }

        public DataTable GetPhoneNumbers(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_PHONE_NUMBERS).Sql, param);
        }

        public DataTable GetEmailAddresses(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_EMAIL_ADDRESSES).Sql, param);
        }

        public List<string> GetProgramCodes() {
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_PROGRAM_CODES).Sql).AsEnumerable().Select(e=>ObjectExtensions.ParseDataRow(e[0])).ToList();
        }

        public List<StudentStatistic> GetStudentStatistics(List<tblInvitation> Invitations, string ProgramCode = null) {
            List<StudentStatistic> retVal = this.GetData<StudentStatistic>(SQL_GET_STUDENT_STATISTICS, Invitations, ProgramCode);
            retVal.ForEach(e => e.SetInvitation(Invitations.Where(f => f.ENumber == e.ENumber).FirstOrDefault()));
            return retVal;
        }

        public List<MailMerge> GetMailMerge(List<tblInvitation> Invitations, string ProgramCode = null) {
            List<MailMerge> retVal = this.GetData<MailMerge>(SQL_GET_MAIL_MERGE, Invitations, ProgramCode);
            retVal.ForEach(e => e.SetInvitation(Invitations.Where(f => f.ENumber == e.ENumber).FirstOrDefault()));
            return retVal;
        }

        public List<InvitedPaid> GetInvitedPaid(List<tblInvitation> Invitations, string Term, string ProgramCode = null) {
            List<InvitedPaid> retVal = this.GetData<InvitedPaid>(SQL_GET_INVITED_PAID, Invitations, Term, ProgramCode);
            retVal.ForEach(e => e.SetInvitation(Invitations.Where(f => f.ENumber == e.ENumber).FirstOrDefault()));
            return retVal;
        }

        public List<EventSummary> GetEventSummary(List<tblInvitation> Invitations, string Term, string ProgramCode = null) {
            List<EventSummary> retVal = this.GetData<EventSummary>(SQL_GET_EVENT_SUMMARY, Invitations, Term, ProgramCode);
            retVal.ForEach(e => e.SetInvitation(Invitations.Where(f => f.ENumber == e.ENumber).FirstOrDefault()));
            return retVal;
        }

        public List<Immunization> GetImmunizationsReport(List<tblInvitation> Invitations, string Term, string ProgramCode = null) {
            List<Immunization> retVal = this.GetData<Immunization>(SQL_GET_IMMUNIZATIONS_REPORT, Invitations);
            retVal.ForEach(e => e.SetInvitation(Invitations.Where(f => f.ENumber == e.ENumber).FirstOrDefault()));
            return retVal;
        }

        public bool HasSelectiveServiceHold(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsScalar(_QUERIES.GetQueryByName(SQL_HAS_SELECTIVE_SERVICE_HOLD).Sql, param) == "Y";
        }

        public bool MustVerifyCitizenship(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsScalar(_QUERIES.GetQueryByName(SQL_MUST_VERIFY_CITIZENSHIP).Sql, param) == "Y";
        }

        public DataTable GetMigrationData() {
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName("GET_LEGACY_MIGRATION_DATA").Sql);
        }

        public List<StudentRequirement> GetAllRequestedRequirements(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_STUDENT_REQUIREMENTS_REQUESTED).Sql, param).AsEnumerable().Select(e => new StudentRequirement(e)).ToList();
        }

        public List<StudentRequirement> GetFinAidChecklistRequirements(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_FIN_AID_CHECKLIST_REQUIREMENTS_REQUESTED).Sql, param).AsEnumerable().Select(e => new StudentRequirement(e)).ToList();
        }

        public List<ImmunizationChecklist> GetImmunizationsChecklist(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsDataTable(_QUERIES.GetQueryByName(SQL_GET_IMMUNIZATION_CHECKLIST).Sql, param).AsEnumerable().Select(e => new ImmunizationChecklist(e)).ToList();
        }

        public string GetFAFSAAidYear(string ENumber) {
            OracleParameter param = new OracleParameter(PARAM_ENUMBER, ENumber);
            return _DB.ExecuteSQLCommandAsScalar(_QUERIES.GetQueryByName(SQL_GET_FAFSA_AID_YEAR).Sql, param);
        }

        #endregion


    }
}