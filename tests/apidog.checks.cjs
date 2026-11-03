const fs = require('node:fs');
const assert = require('node:assert/strict');
const collection = JSON.parse(fs.readFileSync('docs/apidog/FashionStore.postman_collection.json','utf8'));
const env = JSON.parse(fs.readFileSync('docs/apidog/FashionStore.local.postman_environment.json','utf8'));
const items = collection.item.flatMap(folder => folder.item);
let checks = 0;
function check(run) { run(); checks++; }
function script(item, html, code=200) {
  const values = new Map();
  const pm = {environment:{set:(k,v)=>values.set(k,v),unset:k=>values.delete(k)}, response:{code,text:()=>html,json:()=>JSON.parse(html),headers:{get:()=> 'image/png'}}, test:(_,run)=>run(),expect:value=>({to:{eql:expected=>assert.deepEqual(value,expected),include:expected=>assert.ok(value.includes(expected))}})};
  new Function('pm',item.event[0].script.exec.join('\n'))(pm);
  return values;
}
check(()=>assert.equal(items.length,7));
check(()=>assert.equal(env.values.find(v=>v.key==='password').value,''));
check(()=>assert.ok(!JSON.stringify(collection).includes('Bearer')));
check(()=>assert.equal(script(items[0],'<input type="hidden" name="__RequestVerificationToken" value="abc-_123" />').get('loginCsrf'),'abc-_123'));
check(()=>assert.equal(script(items[0],"<input value='abc&amp;123' name='__RequestVerificationToken'>").get('loginCsrf'),'abc&123'));
check(()=>assert.throws(()=>script(items[0],'<html>no token</html>')));
check(()=>script(items[1],'<html>Welcome</html>'));
check(()=>assert.throws(()=>script(items[1],'<input name="Password" />')));
check(()=>assert.equal(script(items[4],'<section data-authenticated="true"><input name="__RequestVerificationToken" value="new-token"></section>').get('tryonCsrf'),'new-token'));
check(()=>assert.throws(()=>script(items[4],'<section data-authenticated="false"><input name="__RequestVerificationToken" value="token"></section>')));
check(()=>script(items[5],JSON.stringify({message:'Hãy chọn ảnh của bạn'}),400));
check(()=>script(items[6],''));
check(()=>assert.ok(items[6].request.body.formdata.some(v=>v.key==='person'&&v.type==='file')));
check(()=>assert.ok(items[6].request.body.formdata.find(v=>v.key==='mask').disabled));

check(()=>assert.equal(env.values.find(v=>v.key==='otpCode').value,''));
check(()=>assert.equal(script(items[2],'<input name="__RequestVerificationToken" value="otp-token">').get('otpCsrf'),'otp-token'));
check(()=>script(items[3],'<html>Welcome</html>'));
check(()=>assert.throws(()=>script(items[3],'<input name="Code">')));
console.log(`Passed ${checks} collection/script checks. No server or provider request was sent.`);
