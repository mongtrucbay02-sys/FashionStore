using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using OutfitStudio.Models;
using OutfitStudio.Services;

var projectRoot=new DirectoryInfo(AppContext.BaseDirectory);
while(projectRoot is not null&&!File.Exists(Path.Combine(projectRoot.FullName,"OutfitStudio.csproj")))projectRoot=projectRoot.Parent;
if(projectRoot is null)throw new InvalidOperationException("Run checks from a source checkout containing OutfitStudio.csproj.");
Directory.SetCurrentDirectory(projectRoot.FullName);
if(args.Contains("--serve-browser-checks")){await BrowserChecks.RunAsync();return;}
int count=0;
void Assert(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS: "+name);}
async Task Reject(Func<Task> action,int status,string name){try{await action();throw new Exception("Expected rejection: "+name);}catch(ImageProcessingException e){Assert(e.StatusCode==status,name);}}
var fixtures=Path.Combine(Directory.GetCurrentDirectory(),"tests","VirtualTryOn.Checks","Fixtures");
var person=new ImageInput(await File.ReadAllBytesAsync(Path.Combine(fixtures,"person.png")),"image/png");
var mask=new ImageInput(await File.ReadAllBytesAsync(Path.Combine(fixtures,"mask.png")),"image/png");
var edited=await File.ReadAllBytesAsync(Path.Combine(fixtures,"edited.png"));
var root=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),".test-output","tryon-contract-files",Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(Path.Combine(root,"wwwroot","images"));Directory.CreateDirectory(Path.Combine(root,"Data"));
var environment=new TestEnvironment(root);var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Storage:Provider"]="Json" }).Build();var store=new AppStore(environment,config);var catalog=new ProductCatalog(store);
var products=new[]{new Product{Id=1,Name="Áo mới",Image="/images/shirt.png",Active=true,Stock=10},new Product{Id=2,Name="Quần mới",Image="/images/pants.png",Active=true,Stock=10},new Product{Id=3,Name="Bộ vest",Image="/images/set.png",Active=true,Stock=10}};
await File.WriteAllTextAsync(Path.Combine(root,"Data","products.json"),JsonSerializer.Serialize(products));
await File.WriteAllTextAsync(Path.Combine(root,"Data","outfits.json"),JsonSerializer.Serialize(new OutfitCatalog{Products=[new(){Id="shirt",StoreProductId=1,Category="shirt",Name="Áo mới",ColorName="Trắng"},new(){Id="pants",StoreProductId=2,Category="pants",Name="Quần mới",ColorName="Xanh"},new(){Id="set",StoreProductId=3,Category="set",Name="Bộ vest"}]}));
foreach(var name in new[]{"shirt","pants","set"})await File.WriteAllBytesAsync(Path.Combine(root,"wwwroot","images",name+".png"),edited);
VirtualTryOnService Service(FakeHandler handler,string key="contract-only"){var factory=new ClientFactory(handler);return new(new CloudflareImageService(factory,Options.Create(new CloudflareAIOptions{ApiKey=key,AccountId="0123456789abcdef0123456789abcdef"}),config),catalog,environment);}
HttpResponseMessage Result()=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new { success=true, result=new { image=Convert.ToBase64String(edited) } }),Encoding.UTF8,"application/json")};
var request=new VirtualTryOnRequest{Source="description",Scope="top",Description="Áo thun trắng rộng"};
try
{
    Assert(VirtualTryOnService.Dimensions(person)==(256,384),"PNG dimensions are read for mask alignment");
    await Reject(()=>Task.Run(()=>VirtualTryOnService.Validate(new(){Source="description"},0)),400,"Description source requires a garment description");
    await Reject(()=>Task.Run(()=>VirtualTryOnService.Validate(new(){Source="upload"},0)),400,"Upload source requires actual reference files");
    await Reject(()=>Task.Run(()=>VirtualTryOnService.Validate(new(){Source="upload"},3)),400,"More than two garment files rejected");
    await Reject(()=>Task.Run(()=>VirtualTryOnService.Validate(new(){Source="description",Description="Áo"},1)),400,"Inactive upload source cannot send extra images");
    await Reject(()=>Task.Run(()=>VirtualTryOnService.Validate(new(){Scope="face"},0)),400,"Unknown edit scope rejected");
    await Reject(()=>Task.Run(()=>VirtualTryOnService.Validate(new(){Source="description",Description=new string('a',1501)},0)),400,"Oversized description rejected");
    await Reject(()=>Task.Run(()=>VirtualTryOnService.Validate(new(){Mode="set",Scope="top"},0)),400,"Full set cannot silently change top-only selection");
    var noNetwork=new FakeHandler((_,_)=>throw new Exception("Provider must not be contacted"));
    await Reject(()=>Service(noNetwork,"").GenerateAsync(request,person,null,[],default),503,"Missing key fails without contacting provider");
    var wrong=new ImageInput(await File.ReadAllBytesAsync(Path.Combine(fixtures,"wrong-mask.png")),"image/png");
    await Reject(()=>Service(noNetwork).GenerateAsync(request,person,wrong,[],default),400,"Mask dimensions must match the first image");
    await Reject(()=>Service(noNetwork).GenerateAsync(request,person,new(edited,"image/png"),[],default),400,"Mask must contain an alpha channel");
    var handler=new FakeHandler(async(http,ct)=>{
        var body=await Form(http,ct);
        Assert(http.RequestUri!.AbsoluteUri=="https://api.cloudflare.com/client/v4/accounts/0123456789abcdef0123456789abcdef/ai/run/@cf/black-forest-labs/flux-2-dev","Official image edit endpoint used");
        Assert(SamePixels(Convert.FromBase64String(body["input_image_0"]),person.Bytes),"Personal photo is the first edit target, not a style reference");
        Assert(body.ContainsKey("input_image_1")&&!SamePixels(Convert.FromBase64String(body["input_image_1"]),person.Bytes)&&!body.ContainsKey("mask"),"Red overlay guide follows person, no unsupported mask field is sent");
        var prompt=body["prompt"];
        Assert(prompt.Contains("same face")&&prompt.Contains("hairstyle")&&prompt.Contains("body shape")&&prompt.Contains("pose")&&prompt.Contains("background")&&prompt.Contains("shoes")&&prompt.Contains("watch")&&prompt.Contains("bag"),"Identity, body, scene and accessory preservation requirements reach provider");
        Assert(prompt.Contains("Use menswear garments only")&&prompt.Contains("Do not change the original person's identity or gender presentation"),"Try-on uses menswear while preserving the original person's identity");
        Assert(prompt.Contains("ONLY the upper-body garment")&&prompt.Contains("original lower-body clothing")&&!prompt.Contains("new generic adult model"),"Top-only edit preserves lower garment and uses the same person");
        Assert(body["width"]=="256"&&body["height"]=="384","One image with automatic framing requested");
        return Result();
    });
    Assert(SamePixels(await Service(handler).GenerateAsync(request,person,mask,[],default),edited),"Actual provider PNG bytes returned without a fake fallback");
    var automatic=new FakeHandler(async(http,ct)=>{var body=await Form(http,ct);Assert(!body.ContainsKey("input_image_1")&&!body.ContainsKey("mask"),"Automatic region mode omits mask instead of sending an invalid null object");return Result();});
    await Service(automatic).GenerateAsync(request,person,null,[],default);
    var catalogRequest=new VirtualTryOnRequest{Source="catalog",Mode="separates",ShirtId="shirt",PantsId="pants"};
    var references=new FakeHandler(async(http,ct)=>{var body=await Form(http,ct);Assert(body.Keys.Count(k=>k.StartsWith("input_image_"))==3,"Catalog mode sends personal photo plus real shirt and pants photos");return Result();});
    await Service(references).GenerateAsync(catalogRequest,person,null,[],default);
    await Reject(()=>Service(noNetwork).GenerateAsync(new(){ShirtId="pants",PantsId="pants"},person,null,[],default),400,"Wrong-category catalog selection rejected");
    products[0].Active=false;await File.WriteAllTextAsync(Path.Combine(root,"Data","products.json"),JsonSerializer.Serialize(products));
    await Reject(()=>Service(noNetwork).GenerateAsync(catalogRequest,person,null,[],default),400,"Hidden product cannot be sent to provider");
    products[0].Active=true;products[0].Image="/images/../../private.png";await File.WriteAllTextAsync(Path.Combine(root,"Data","products.json"),JsonSerializer.Serialize(products));
    await Reject(()=>Service(noNetwork).GenerateAsync(catalogRequest,person,null,[],default),400,"Image path traversal rejected");
    var upload=new FakeHandler(async(http,ct)=>{var body=await Form(http,ct);Assert(body.Keys.Count(k=>k.StartsWith("input_image_"))==2,"Uploaded garment reference follows personal photo");return Result();});
    await Service(upload).GenerateAsync(new(){Source="upload",Scope="bottom"},person,null,[new(edited,"image/png")],default);
    Assert(!Directory.Exists(Path.Combine(root,"App_Data")),"Service stores neither personal inputs nor results on disk");
    Console.WriteLine($"Completed {count} try-on checks using simulated provider transport. Image quality was not evaluated.");
}
finally{var allowed=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),".test-output","tryon-contract-files"))+Path.DirectorySeparatorChar;if(root.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}


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

sealed class ClientFactory(FakeHandler handler):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(handler,false);}
sealed class FakeHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> run):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>run(request,ct);}
sealed class TestEnvironment(string root):IWebHostEnvironment
{
    public string WebRootPath{get;set;}=Path.Combine(root,"wwwroot");public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public string ContentRootPath{get;set;}=root;public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="VirtualTryOn.Checks";
}


