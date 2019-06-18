using System;
using System.Web;
using System.Web.Configuration;
using Core;

public class CheckUserHandler : IHttpHandler
{

	public void ProcessRequest(HttpContext context)
	{
        string username = context.Request.Params["username"];
        string psd = context.Request.Params["psd"];
        if (Core.AccountImpl.Instance.Validate(username, psd))
        {
            context.Response.Write("OK");
        }
        else
        {
            context.Response.Write("FALSE");
        }

    }

	public bool IsReusable
	{
		get
		{
			return false;
		}
	}

}