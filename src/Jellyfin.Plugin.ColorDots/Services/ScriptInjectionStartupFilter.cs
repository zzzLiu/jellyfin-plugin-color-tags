using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ColorDots.Services;

public sealed class ScriptInjectionStartupFilter : IStartupFilter
{
	private readonly ILogger<ScriptInjectionStartupFilter> _logger;

	private int _loggedOnce;

	public ScriptInjectionStartupFilter(ILogger<ScriptInjectionStartupFilter> logger)
	{
		_logger = logger;
	}

	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		return (IApplicationBuilder app) =>
		{
			app.Use(InvokeAsync);
			next(app);
		};
	}

	internal static bool IsIndexRequest(string? path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		if (path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/web", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		int num = path.LastIndexOf("/web/", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return false;
		}
		string text = path.Substring(num + "/web/".Length);
		if (text.Length == 0 || text.Contains('.', StringComparison.Ordinal))
		{
			return false;
		}
		return !text.StartsWith("ConfigurationPage", StringComparison.OrdinalIgnoreCase);
	}

	private async Task InvokeAsync(HttpContext context, Func<Task> next)
	{
		if (!IsIndexRequest(context.Request.Path.Value) || !HttpMethods.IsGet(context.Request.Method))
		{
			await next().ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		context.Request.Headers.Remove("Accept-Encoding");
		context.Request.Headers.Remove("Range");
		context.Request.Headers.Remove("If-Range");
		context.Request.Headers.Remove("If-None-Match");
		context.Request.Headers.Remove("If-Modified-Since");
		Stream originalBody = context.Response.Body;
		using MemoryStream buffer = new MemoryStream();
		context.Response.Body = buffer;
		try
		{
			await next().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
			context.Response.Body = originalBody;
			throw;
		}
		context.Response.Body = originalBody;
		buffer.Seek(0L, SeekOrigin.Begin);
		string text = context.Response.ContentType ?? string.Empty;
		if (context.Response.StatusCode != 200 || (!text.Contains("text/html", StringComparison.OrdinalIgnoreCase) && text.Length != 0 && !text.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase)))
		{
			await buffer.CopyToAsync(originalBody).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		string text2;
		using (StreamReader reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true))
		{
			text2 = await reader.ReadToEndAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		if (text2.IndexOf("</body>", StringComparison.OrdinalIgnoreCase) < 0)
		{
			buffer.Seek(0L, SeekOrigin.Begin);
			await buffer.CopyToAsync(originalBody).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		try
		{
			string text3 = context.Request.PathBase.Value ?? string.Empty;
			if (text3.EndsWith('/'))
			{
				text3 = text3.TrimEnd('/');
			}
			string text4 = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1";
			text2 = Inject(text2, text3 + "/ColorDots/client.js?v=" + text4);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Jellyfin Color Tags left index.html unchanged after an injection error.");
		}
		byte[] bytes = Encoding.UTF8.GetBytes(text2);
		context.Response.ContentType = "text/html; charset=utf-8";
		context.Response.ContentLength = bytes.Length;
		context.Response.Headers.Remove("ETag");
		context.Response.Headers.Remove("Last-Modified");
		context.Response.Headers.Remove("Accept-Ranges");
		context.Response.Headers.Remove("Content-Encoding");
		context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
		context.Response.Headers.Pragma = "no-cache";
		await originalBody.WriteAsync(bytes).ConfigureAwait(continueOnCapturedContext: false);
	}

	private string Inject(string html, string scriptSrc)
	{
		if (html.Contains("/ColorDots/client.js", StringComparison.OrdinalIgnoreCase))
		{
			return html;
		}
		int num = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return html;
		}
		string text = "<script src=\"" + scriptSrc + "\" defer data-plugin=\"ColorDots\"></script>";
		if (Interlocked.Exchange(ref _loggedOnce, 1) == 0)
		{
			_logger.LogInformation("Jellyfin Color Tags injected its player script into the web client response.");
		}
		return html.Substring(0, num) + text + "\n" + html.Substring(num);
	}
}
