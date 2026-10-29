# jigger-jot's own mails, parked

JiggerJot's transactional mails are its own design (the display serif embedded, the lockup and Marga attached per mail, its card layout, its strings; `EmailLookTests`). On the platform's packages they need a seam to replace the platform's renderer: perezosoft-platform#462. Until it lands these files are kept here, not compiled (`JiggerJot.Host.csproj`), and the app sends the platform's mails branded through `options.Brand`.
