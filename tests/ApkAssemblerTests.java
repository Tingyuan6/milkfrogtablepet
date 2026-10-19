import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.zip.*;

public final class ApkAssemblerTests {
    private static int checks;

    static void expectFailure(RunnableIO action) throws Exception {
        try {
            action.run();
        } catch (IOException expected) {
            checks++;
            return;
        }
        throw new AssertionError("Expected invalid APK to fail verification");
    }

    interface RunnableIO {
        void run() throws Exception;
    }

    public static void main(String[] args) throws Exception {
        Path root = Files.createTempDirectory("naifrog-apk-test-");
        try {
            Path assets = Files.createDirectories(root.resolve("assets"));
            Files.createDirectories(assets.resolve("poses"));
            Files.createDirectories(assets.resolve("audio"));
            byte[] image = {1, 2, 3, 4}, audio = {5, 6, 7, 8};
            Files.write(assets.resolve("poses/idle.png"), image);
            Files.write(assets.resolve("audio/andi.wav"), audio);
            Files.writeString(assets.resolve("manifest.json"), "{}");
            Path dex = root.resolve("classes.dex");
            Files.write(dex, new byte[] {9});
            Path broken = root.resolve("old.apk"), fixed = root.resolve("new.apk");
            try (ZipOutputStream out = new ZipOutputStream(Files.newOutputStream(broken))) {
                ApkAssembler.write(out, "assets/poses\\\\idle.png", image, ZipEntry.STORED, 0);
                ApkAssembler.write(out, "assets/audio\\\\andi.wav", audio, ZipEntry.STORED, 0);
                ApkAssembler.write(
                        out, "assets/manifest.json", new byte[] {123, 125}, ZipEntry.DEFLATED, 0);
            }
            expectFailure(() -> ApkAssembler.verify(broken, assets));
            ApkAssembler.assemble(broken, dex, fixed);
            ApkAssembler.verify(fixed, assets);
            checks++;
            try (ZipFile zip = new ZipFile(fixed.toFile())) {
                if (zip.getEntry("assets/poses/idle.png") == null
                        || zip.getEntry("assets/audio/andi.wav").getMethod() != ZipEntry.STORED)
                    throw new AssertionError("Canonical path or audio storage lost");
                checks++;
            }
            Files.write(assets.resolve("poses/idle.png"), new byte[] {0});
            expectFailure(() -> ApkAssembler.verify(fixed, assets));
            Files.write(assets.resolve("poses/idle.png"), image);
            Files.write(assets.resolve("poses/missing.png"), image);
            expectFailure(() -> ApkAssembler.verify(fixed, assets));
            Path duplicate = root.resolve("duplicate.apk");
            try (ZipOutputStream out = new ZipOutputStream(Files.newOutputStream(duplicate))) {
                ApkAssembler.write(out, "assets/poses/idle.png", image, ZipEntry.STORED, 0);
                ApkAssembler.write(out, "assets/poses\\idle.png", image, ZipEntry.STORED, 0);
            }
            expectFailure(
                    () -> ApkAssembler.assemble(duplicate, dex, root.resolve("rejected.apk")));
            expectFailure(() -> ApkAssembler.canonical("assets/../secret"));
            System.out.println("APK packaging regression checks: " + checks + " passed.");
        } finally {
            try (java.util.stream.Stream<Path> paths = Files.walk(root)) {
                for (Path path : (Iterable<Path>) paths.sorted(Comparator.reverseOrder())::iterator)
                    Files.delete(path);
            }
        }
    }
}
