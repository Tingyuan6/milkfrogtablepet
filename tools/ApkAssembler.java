import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.zip.*;

/** SDK-only APK assembly. ZIP paths must use '/', including when aapt2 runs on Windows. */
public final class ApkAssembler {
    public static void main(String[] args) throws Exception {
        if (args.length == 4 && args[0].equals("assemble")) {
            assemble(Paths.get(args[1]), Paths.get(args[2]), Paths.get(args[3]));
        } else if (args.length == 3 && args[0].equals("verify")) {
            verify(Paths.get(args[1]), Paths.get(args[2]));
        } else {
            throw new IllegalArgumentException(
                    "assemble resources.apk classes.dex output.apk | verify signed.apk"
                        + " assets-directory");
        }
    }

    static String canonical(String name) throws IOException {
        String result = name.replace('\\', '/').replaceAll("/{2,}", "/");
        if (result.startsWith("/") || result.contains(":"))
            throw new IOException("Unsafe ZIP path: " + name);
        for (String part : result.split("/")) {
            if (part.equals(".") || part.equals(".."))
                throw new IOException("Unsafe ZIP path: " + name);
        }
        return result;
    }

    static void assemble(Path resources, Path dex, Path output) throws IOException {
        Set<String> names = new HashSet<>();
        int normalized = 0;
        try (ZipFile input = new ZipFile(resources.toFile());
                ZipOutputStream out = new ZipOutputStream(Files.newOutputStream(output))) {
            Enumeration<? extends ZipEntry> entries = input.entries();
            while (entries.hasMoreElements()) {
                ZipEntry entry = entries.nextElement();
                String name = canonical(entry.getName());
                if (!names.add(name)) throw new IOException("Duplicate ZIP path: " + name);
                if (!name.equals(entry.getName())) normalized++;
                try (InputStream in = input.getInputStream(entry)) {
                    write(out, name, in.readAllBytes(), entry.getMethod(), entry.getTime());
                }
            }
            if (!names.add("classes.dex"))
                throw new IOException("Unexpected classes.dex in resources APK");
            write(out, "classes.dex", Files.readAllBytes(dex), ZipEntry.DEFLATED, 0);
        }
        System.out.println("APK assembled; normalized " + normalized + " Windows ZIP paths.");
    }

    static void write(ZipOutputStream out, String name, byte[] bytes, int method, long time)
            throws IOException {
        ZipEntry entry = new ZipEntry(name);
        entry.setMethod(method);
        entry.setTime(Math.max(0, time));
        if (method == ZipEntry.STORED) {
            CRC32 crc = new CRC32();
            crc.update(bytes);
            entry.setSize(bytes.length);
            entry.setCompressedSize(bytes.length);
            entry.setCrc(crc.getValue());
        }
        out.putNextEntry(entry);
        out.write(bytes);
        out.closeEntry();
    }

    static void verify(Path apk, Path assets) throws IOException {
        Map<String, byte[]> local = new HashMap<>();
        // Inspect raw local names, rather than a Windows ZIP library's normalized names.
        try (ZipInputStream in = new ZipInputStream(Files.newInputStream(apk))) {
            ZipEntry entry;
            while ((entry = in.getNextEntry()) != null) {
                String name = entry.getName();
                if (!name.equals(canonical(name)))
                    throw new IOException("Non-portable APK path: " + name);
                if (local.put(name, in.readAllBytes()) != null)
                    throw new IOException("Duplicate APK path: " + name);
            }
        }
        int checked = 0;
        try (ZipFile zip = new ZipFile(apk.toFile());
                java.util.stream.Stream<Path> files = Files.walk(assets)) {
            Set<String> central = new HashSet<>();
            Enumeration<? extends ZipEntry> entries = zip.entries();
            while (entries.hasMoreElements()) {
                ZipEntry entry = entries.nextElement();
                String name = entry.getName();
                if (!name.equals(canonical(name)) || !central.add(name))
                    throw new IOException("Invalid central ZIP path: " + name);
                if (!local.containsKey(name))
                    throw new IOException("Local/central ZIP path mismatch: " + name);
            }
            if (!central.equals(local.keySet())) throw new IOException("ZIP directory mismatch");
            for (Path file : (Iterable<Path>) files.filter(Files::isRegularFile)::iterator) {
                String name = "assets/" + assets.relativize(file).toString().replace('\\', '/');
                ZipEntry entry = zip.getEntry(name);
                if (entry == null) throw new IOException("Missing APK asset: " + name);
                byte[] expected = Files.readAllBytes(file);
                try (InputStream in = zip.getInputStream(entry)) {
                    if (!Arrays.equals(expected, in.readAllBytes())
                            || !Arrays.equals(expected, local.get(name)))
                        throw new IOException("APK asset bytes differ: " + name);
                }
                if (name.endsWith(".wav") && entry.getMethod() != ZipEntry.STORED)
                    throw new IOException("Audio must be stored for AssetManager.openFd: " + name);
                checked++;
            }
        }
        if (!local.containsKey("classes.dex")) throw new IOException("Missing classes.dex");
        System.out.println(
                "APK verified: "
                        + checked
                        + " assets, exact bytes and raw local/central paths; WAV stored.");
    }
}
