using System.Net;

using System.Text;

using System.Text.Json;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Options;

using OutfitStudio.Services;

using var testBitmap=new SkiaSharp.SKBitmap(16,16);

testBitmap.Erase(SkiaSharp.SKColors.White);

using var testEncoded=testBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);

var png=testEncoded.ToArray();

var config = new ConfigurationBuilder().Build();

const string editPrompt = "Replace only the shirt with a white menswear shirt; preserve the same person and background.";
int passed = 0;

void Assert(bool value, string name) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS: " + name); }

CloudflareImageService Service(FakeHandler handler, string key = "contract-test-only") => new(new ClientFactory(handler), Options.Create(new CloudflareAIOptions { ApiKey = key, AccountId = "0123456789abcdef0123456789abcdef" }), config);

async Task Reject(Func<Task> action, int status, string name)

{

    try { await action(); throw new Exception("Expected rejection: " + name); }

    catch (ImageProcessingException exception) { Assert(exception.StatusCode == status, name); }

}

var image = await CloudflareImageService.ReadImageAsync(new FormFile(new MemoryStream(png),0,png.Length,"image","picture.txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" }, default);

Assert(image.MediaType == "image/png", "Image type is determined from bytes rather than filename/MIME");

await Reject(async () => { await CloudflareImageService.ReadImageAsync(null, default); },400,"Missing image rejected");

await Reject(async () => { await CloudflareImageService.ReadImageAsync(new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("<svg/>")),0,6,"image","fake.png"),default); },400,"Disguised SVG rejected");

await Reject(async () => { await CloudflareImageService.ReadImageAsync(new FormFile(new MemoryStream(),0,CloudflareImageService.MaxUploadBytes+1,"image","large.png"),default); },400,"Oversized upload rejected");

var missing = new FakeHandler((_,_) => throw new Exception("Network must not be called without key"));

await Reject(() => Service(missing, "").EditImagesAsync([image],editPrompt,"1024x1024",default),503,"Missing key fails before network");

var otherProvider = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["OPENAI_API_KEY"] = "other-provider-only", ["GEMINI_API_KEY"]="other-only" }).Build();

Assert(!new CloudflareImageService(new ClientFactory(missing),Options.Create(new CloudflareAIOptions()),otherProvider).IsConfigured,"Old provider keys cannot configure Cloudflare");

var cloudflare = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["CLOUDFLARE_API_TOKEN"] = "test-only",["CLOUDFLARE_ACCOUNT_ID"]="0123456789abcdef0123456789abcdef" }).Build();

Assert(new CloudflareImageService(new ClientFactory(missing),Options.Create(new CloudflareAIOptions()),cloudflare).IsConfigured,"Cloudflare token and account recognized");

await Reject(()=>new CloudflareImageService(new ClientFactory(missing),Options.Create(new CloudflareAIOptions{ApiKey="test-only"}),config).EditImagesAsync([image],editPrompt,"1024x1024",default),503,"Missing Account ID rejected without network");

await Reject(()=>new CloudflareImageService(new ClientFactory(missing),Options.Create(new CloudflareAIOptions{ApiKey="test-only",AccountId="../invalid"}),config).EditImagesAsync([image],editPrompt,"1024x1024",default),503,"Invalid Account ID rejected without network");

var contract = new FakeHandler(async (request, cancellationToken) =>

{

    Assert(request.RequestUri?.AbsoluteUri == "https://api.cloudflare.com/client/v4/accounts/0123456789abcdef0123456789abcdef/ai/run/@cf/black-forest-labs/flux-2-dev", "Only official image edit endpoint is called");

    Assert(request.Method == HttpMethod.Post && request.Headers.Authorization?.Scheme == "Bearer", "Authenticated POST is used");

    var body=await Form(request,cancellationToken);

    Assert(SamePixels(Convert.FromBase64String(body["input_image_0"]),png), "Source image bytes are sent as reference");

    Assert(body["width"]=="512" && body["height"]=="512" && body["steps"]=="20", "Bounded square output and step count requested");

    Assert(body["prompt"].Contains(editPrompt), "Requested clothing preservation prompt is included");

    return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { success=true, result=new { image=Convert.ToBase64String(png) } }));

});

Assert(SamePixels(await Service(contract).EditImagesAsync([image],editPrompt,"1024x1024",default),png), "Provider result is decoded without substituting another image");

foreach (var invalid in new[] { "[]", "{}", "{\"success\":false,\"result\":{\"image\":\"bad!\"}}", "{\"success\":true,\"result\":{\"image\":\"bad!\"}}", "{\"success\":true,\"result\":{\"image\":\"PHN2Zy8+\"}}" })

    await Reject(() => Service(new FakeHandler((_,_) => Task.FromResult(Json(HttpStatusCode.OK,invalid)))).EditImagesAsync([image],editPrompt,"1024x1024",default),502,"Malformed or failed Cloudflare output rejected");

using(var jpegBitmap=new SkiaSharp.SKBitmap(32,48))

{

    jpegBitmap.Erase(SkiaSharp.SKColors.Blue); using var jpeg=jpegBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg,90);

    var jpegHandler=new FakeHandler((_,_)=>Task.FromResult(Json(HttpStatusCode.OK,JsonSerializer.Serialize(new{success=true,result=new{image=Convert.ToBase64String(jpeg.ToArray())}}))));

    var converted=await Service(jpegHandler).EditImagesAsync([image],editPrompt,"1024x1024",default);

    using var actual=SkiaSharp.SKBitmap.Decode(converted);

    Assert(CloudflareImageService.DetectMediaType(converted)=="image/png"&&actual.Width==32&&actual.Height==48,"JPEG provider result is converted to a real PNG");

}

using(var largeBitmap=new SkiaSharp.SKBitmap(1000,1500))

{

    largeBitmap.Erase(SkiaSharp.SKColors.Green); using var large=largeBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);

    var resizeHandler=new FakeHandler(async(http,ct)=>{

        var values=await Form(http,ct);using var actual=SkiaSharp.SKBitmap.Decode(Convert.FromBase64String(values["input_image_0"]));

        Assert(actual.Width==341&&actual.Height==512,"Reference fits 512px with original aspect ratio");

        return Json(HttpStatusCode.OK,JsonSerializer.Serialize(new{success=true,result=new{image=Convert.ToBase64String(png)}}));

    });

    await Service(resizeHandler).EditImagesAsync([new(large.ToArray(),"image/png")],editPrompt,"1024x1024",default);

}

await Reject(() => Service(new FakeHandler((_,_) => Task.FromResult(Json(HttpStatusCode.Unauthorized,"credential must never be shown")))).EditImagesAsync([image],editPrompt,"1024x1024",default),502,"Provider authentication error is sanitized");

await Reject(() => Service(new FakeHandler((_,_) => Task.FromResult(Json(HttpStatusCode.TooManyRequests,"{}")))).EditImagesAsync([image],editPrompt,"1024x1024",default),429,"Quota error surfaced without retrying paid generation");

await Reject(() => Service(new FakeHandler((_,_) => throw new TaskCanceledException())).EditImagesAsync([image],editPrompt,"1024x1024",default),504,"Timeout reported without success");

var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

var serial = Service(new FakeHandler((_,_) => release.Task));

var first = serial.EditImagesAsync([image],editPrompt,"1024x1024",default);

await Reject(() => serial.EditImagesAsync([image],editPrompt,"1024x1024",default),429,"Concurrent paid generation is blocked");

release.SetResult(Json(HttpStatusCode.OK,JsonSerializer.Serialize(new { success=true, result=new { image=Convert.ToBase64String(png) } })));

await first;

Console.WriteLine($"Completed {passed} checks. Transport was simulated; no Cloudflare call or image-quality assertion was made.");

static HttpResponseMessage Json(HttpStatusCode code, string text) => new(code) { Content = new StringContent(text,Encoding.UTF8,"application/json") };

static async Task<Dictionary<string,string>> Form(HttpRequestMessage request,CancellationToken ct)

{

    if(request.Content is not MultipartFormDataContent multipart)throw new Exception("Expected multipart form data");

    var values=new Dictionary<string,string>();

    foreach(var part in multipart)

    {

        var name=part.Headers.ContentDisposition!.Name!.Trim('"');

        values.Add(name,part.Headers.ContentDisposition.FileName is null?await part.ReadAsStringAsync(ct):Convert.ToBase64String(await part.ReadAsByteArrayAsync(ct)));

    }

    return values;

}

static bool SamePixels(byte[] first,byte[] second)

{

    using var a=SkiaSharp.SKBitmap.Decode(first);using var b=SkiaSharp.SKBitmap.Decode(second);

    return a is not null && b is not null && a.Width==b.Width && a.Height==b.Height && a.Pixels.SequenceEqual(b.Pixels);

}

sealed class ClientFactory(FakeHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, disposeHandler:false); }

sealed class FakeHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> run) : HttpMessageHandler

{

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) => run(request,cancellationToken);

}

