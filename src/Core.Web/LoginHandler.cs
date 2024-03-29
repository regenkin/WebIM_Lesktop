﻿using System;
using System.Web;
using System.Web.Configuration;
using Core;

public class LoginHandler : IHttpHandler
{

	public void ProcessRequest(HttpContext context)
	{
		AccountInfo cu = ServerImpl.Instance.GetCurrentUser(context);
		if (cu != null)
		{
			ServerImpl.Instance.Logout(context);
			string sessionId = context.Request.Params["SessionID"];
			SessionManagement.Instance.RemoveSession(cu.ID, sessionId);
			string data = Utility.RenderHashJson(
				"User", cu.ID,
				"State", SessionManagement.Instance.IsOnline(cu.ID) ? "Online" : "Offline",
				"Details", cu.DetailsJson
			);
			SessionManagement.Instance.Send("UserStateChanged", data);
		}
		if (context.Request.Params["token"] != null)
		{
			
		}
		else
		{
            context.Response.Redirect("Client.html");

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