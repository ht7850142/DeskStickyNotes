// Run through ego-browser nodejs. Pass the existing TaskSpace ID for this goal.
// Browser setup and teardown belong to the caller; never launch another browser.
export async function exportPressKit({spaceId, assetDirectory}) {
  const task = await taskSpace(spaceId);
  const page = task.page('p1');
  const fs = await import('node:fs/promises');
  const {pathToFileURL} = await import('node:url');
  const output = assetDirectory.replace(/\/$/, '') + '/exports/';
  const base = pathToFileURL(assetDirectory.replace(/\/$/, '') + '/press-kit.html').href;
  await fs.mkdir(output, {recursive:true});
  const jobs = [];
  for (const lang of ['zh','en']) {
    for (const scene of ['hero','notes','shortcuts','tray','languages'])
      jobs.push({lang,scene,format:'ph',width:1270,height:760,name:`${lang}-${scene}-1270x760.png`});
    jobs.push({lang,scene:'hero',format:'social',width:1280,height:640,name:`${lang}-social-1280x640.png`});
    jobs.push({lang,scene:'hero',format:'wide',width:1920,height:1080,name:`${lang}-wide-1920x1080.png`});
  }
  jobs.push({lang:'zh',scene:'hero',format:'portrait',width:1080,height:1440,name:'zh-poster-1080x1440.png'});
  jobs.push({lang:'en',scene:'hero',format:'itch',width:630,height:500,name:'en-itch-cover-630x500.png'});
  jobs.push({lang:'en',scene:'logo',format:'ph',width:240,height:240,name:'thumbnail-240x240.png'});
  jobs.push({lang:'en',scene:'logo',format:'large',width:512,height:512,name:'icon-512x512.png'});
  const manifest = [];
  for (const job of jobs) {
    await page.cdp('Emulation.setDeviceMetricsOverride', {width:job.width,height:job.height,deviceScaleFactor:1,mobile:false});
    await page.goto(`${base}?scene=${job.scene}&lang=${job.lang}&format=${job.format}`);
    await page.waitForFunction(() => document.fonts.status==='loaded' && [...document.images].every(i=>i.complete&&i.naturalWidth>0));
    await page.evaluate(async () => {
      await document.fonts.ready;
      await Promise.all([...document.images].map(i=>i.decode()));
      await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
    });
    const layout = await page.evaluate(() => {
      const art = document.querySelector('.wide-canvas,.art');
      const copy = document.querySelector('.copy')?.getBoundingClientRect();
      const portraitNote = document.querySelector('.portrait .main-note')?.getBoundingClientRect();
      return {width:art.offsetWidth,height:art.offsetHeight,
        overflow:[...document.querySelectorAll('.copy h1,.copy p,.edition')].some(el=>el.scrollWidth>el.clientWidth+1),
        portraitOverlap:copy&&portraitNote?copy.bottom+20>portraitNote.top:false};
    });
    if (layout.width!==job.width || layout.height!==job.height || layout.overflow || layout.portraitOverlap)
      throw new Error(JSON.stringify({job,layout}));
    await page.screenshot({path:output+job.name});
    manifest.push({...job,bytes:(await fs.stat(output+job.name)).size});
    console.log(job.name);
  }
  await fs.writeFile(output+'manifest.json',JSON.stringify({product:'DeskStickyNotes',version:'0.6.0',date:'2026-11-06',images:manifest},null,2)+'\n','utf8');
  return manifest;
}
