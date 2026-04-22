// AOT canary. In Phase 0 this is a stub; from Phase 1 onward it exercises the
// library's public surface so the AOT analyzer sees the full call graph and
// the PublishAot build in CI catches any AOT regression.

Console.WriteLine("Expreszo AOT canary: OK");
