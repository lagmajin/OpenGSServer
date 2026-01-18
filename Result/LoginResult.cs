using Newtonsoft.Json.Linq;


namespace OpenGSServer
{
    public enum eLoginResultType
    {
        Unknown,
        LoginSucceeded,
        AccountNotFound,
        InvalidIDorPassword,
        AlreadyLogonSameUser,

    }

    public class LoginResult : AbstractResult
    {
        public readonly bool succeeded = false;

        readonly string reason = "";

        private readonly string id_ = "";

        eLoginResultType type_ = eLoginResultType.Unknown;

        public LoginResult(in string id, eLoginResultType type = eLoginResultType.Unknown)
        {
            this.type_ = type;
            this.id_ = id;

            if (eLoginResultType.LoginSucceeded == type)
            {
                succeeded = true;
            }

            //if(eLoginResultType.)

        }



        public LoginResult(bool succeeded, eLoginResultType type = eLoginResultType.Unknown)
        {
            this.succeeded = succeeded;
            this.type_ = type;
        }
        public string MessageType()
        {
            switch (type_)
            {
                case eLoginResultType.Unknown:
                    return "Unknown";
                case eLoginResultType.LoginSucceeded:
                    return "LoginSucceeded";
                case eLoginResultType.AccountNotFound:
                    return "AccountNotFound";
                case eLoginResultType.InvalidIDorPassword:
                    return "Invalid ID or password";
                case eLoginResultType.AlreadyLogonSameUser:
                    return "AlreadyLogonSameUser";
            }
            return "";
        }

        private string Message()
        {
            switch (type_)
            {
                case eLoginResultType.Unknown:
                    return "Unknown Message";
                case eLoginResultType.LoginSucceeded:
                    return "Login Successful";
                case eLoginResultType.AccountNotFound:
                    return "Account Not Found";
                case eLoginResultType.InvalidIDorPassword:
                    return "Invalid ID or Password";
                case eLoginResultType.AlreadyLogonSameUser:
                    return "Already logged in same user";
            }
            return "";
        }

        public JObject ToJson()
        {
            var result = new JObject();

            if (succeeded)
            {
                result["MessageType"] = "LoginSuccessful";
                result["AccountID"] = id_;




            }
            else
            {
                result["MessageType"] = "LoginFailure";
            }


            result["Message"] = Message();


            return result;
        }

    }
}
