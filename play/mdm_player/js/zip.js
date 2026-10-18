/* MDM = 普通 zip。只支持 stored(0) / deflate(8)，不支持 zip64 与加密，足够覆盖 MdmPacker 与 Python zipfile 的产物。 */
(function (MDP) {
  "use strict";

  const SIG_EOCD = 0x06054b50;
  const SIG_CEN = 0x02014b50;
  const SIG_LOC = 0x04034b50;

  function findEocd(view) {
    const min = Math.max(0, view.byteLength - 22 - 0xffff);
    for (let p = view.byteLength - 22; p >= min; p--) {
      if (view.getUint32(p, true) === SIG_EOCD) return p;
    }
    throw new Error("不是有效的 zip / mdm 文件（找不到中央目录）");
  }

  async function inflateRaw(bytes) {
    if (typeof DecompressionStream === "undefined")
      throw new Error("浏览器不支持 DecompressionStream，请使用新版 Chrome / Edge / Firefox");
    const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream("deflate-raw"));
    return new Uint8Array(await new Response(stream).arrayBuffer());
  }

  const utf8 = new TextDecoder("utf-8");

  /** 返回 Map<小写文件名, {name, read(): Promise<Uint8Array>}>；与 MdmPacker.Unpack 一致，目录层级被拍平。 */
  function readZip(buffer) {
    const view = new DataView(buffer);
    const eocd = findEocd(view);
    const count = view.getUint16(eocd + 10, true);
    let p = view.getUint32(eocd + 16, true);
    const entries = new Map();

    for (let i = 0; i < count; i++) {
      if (view.getUint32(p, true) !== SIG_CEN) throw new Error("zip 中央目录损坏");
      const method = view.getUint16(p + 10, true);
      const compSize = view.getUint32(p + 20, true);
      const nameLen = view.getUint16(p + 28, true);
      const extraLen = view.getUint16(p + 30, true);
      const commentLen = view.getUint16(p + 32, true);
      const localOffset = view.getUint32(p + 42, true);
      const fullName = utf8.decode(new Uint8Array(buffer, p + 46, nameLen));
      p += 46 + nameLen + extraLen + commentLen;

      if (fullName.endsWith("/")) continue;
      const name = fullName.split(/[\\/]/).pop();
      if (!name) continue;

      entries.set(name.toLowerCase(), {
        name,
        async read() {
          if (view.getUint32(localOffset, true) !== SIG_LOC) throw new Error("zip 本地头损坏: " + name);
          const ln = view.getUint16(localOffset + 26, true);
          const le = view.getUint16(localOffset + 28, true);
          const start = localOffset + 30 + ln + le;
          const raw = new Uint8Array(buffer, start, compSize);
          if (method === 0) return raw.slice();
          if (method === 8) return inflateRaw(raw);
          throw new Error(`不支持的压缩方式 ${method}: ${name}`);
        }
      });
    }
    return entries;
  }

  MDP.readZip = readZip;
})(window.MDP = window.MDP || {});
