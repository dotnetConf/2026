# Killing Session Theft: Device Bound Session Credentials using ASP.NET Core 11

by [Wesley Cabus](https://wesleycabus.be)

- [LinkedIn](https://www.linkedin.com/in/wcabus/)
- [BlueSky](https://bsky.app/profile/gotsharp.be)
- [GitHub](https://github.com/wcabus)

## Abstract

Infostealer malware doesn't need your password if it can steal your authentication cookie from disk or memory, and replay 
it on another machine. Any webserver happily accepts the stolen cookie since it was issued by the webserver itself. 
This type of attack bypasses WebAuthn or MFA entirely, since the cookie gets stolen after you have authenticated. 
Since there's no real fix against this, we can only issue cookies with a shorter lifetime to minimize the attack window. 

Until now. Device Bound Session Credentials (DBSC) is a W3C draft introducing a solution, by tying the session cookie to 
the browser or device. The browser generates a private/public key pair, storing the private key ideally in secure storage 
like a TPM, while the webserver only issues short-lived authentication cookies. When the cookie expires, the browser can 
then only refresh the cookie by signing a server-issued challenge using that private device-specific key. 
While this doesn't prevent cookie and session theft, it does minimize the risk since cookies go stale faster and cannot 
be renewed without the device's private key. 

During this session, I'll demonstrate how DBSC works by showing an example implementation in ASP.NET Core, adding the 
necessary endpoints to safely register and refresh DBSC-enabled session cookies.

## Resources

You can find the [slides here](./slides.pdf), and the sample code in the GitHub repository [wcabus/dbsc-aspnetcore](https://github.com/wcabus/dbsc-aspnetcore).

### Additional Resources

- Draft spec: https://w3c.github.io/webappsec-dbsc/
- Scott Helme's blog posts on DBSC: 
  - https://scotthelme.co.uk/device-bound-session-credentials-making-stolen-cookies-useless/
  - https://scotthelme.co.uk/everything-i-learned-shipping-device-bound-session-credentials/
- Andrew Lock's blos posts on DBSC:
  - https://andrewlock.net/understanding-device-bound-session-credentials/
  - https://andrewlock.net/exploring-the-dotnet-11-preview-8-experimental-support-for-device-bound-session-credentials-in-aspnetcore/
- Maarten Balliauw's blog post on DBSC: https://blog.maartenballiauw.be/posts/2026-09-16-device-bound-session-credentials-in-aspnet-core/