using System;
using System.Collections.Generic;

namespace CraftConsole.CrossPlatform
{
    public static class Catalog
    {
        public static List<EffectItem> GetDefaultCatalog()
        {
            var list = new List<EffectItem>();

            void AddEff(string name, string id, string cat, string? keywords = null)
            {
                list.Add(new EffectItem
                {
                    Name = name,
                    Id = id,
                    Category = cat,
                    Kind = ConsoleItemKind.Effect,
                    Keywords = keywords,
                    LowerName = name.ToLowerInvariant(),
                    LowerCategory = cat.ToLowerInvariant(),
                    LowerKeywords = (keywords ?? "").ToLowerInvariant()
                });
            }

            void AddPre(string name, string id, string cat)
            {
                list.Add(new EffectItem
                {
                    Name = name,
                    Id = id,
                    Category = cat,
                    Kind = ConsoleItemKind.Preset,
                    IsPreset = true,
                    LowerName = name.ToLowerInvariant(),
                    LowerCategory = cat.ToLowerInvariant()
                });
            }

            void AddLayer(string name, string id, string cat, string? shortcut = null)
            {
                list.Add(new EffectItem
                {
                    Name = name,
                    Id = id,
                    Category = cat,
                    Kind = ConsoleItemKind.Layer,
                    Extra = shortcut,
                    LowerName = name.ToLowerInvariant(),
                    LowerCategory = cat.ToLowerInvariant()
                });
            }

            void AddCommand(string name, string id, string cat, string? shortcut = null)
            {
                list.Add(new EffectItem
                {
                    Name = name,
                    Id = id,
                    Category = cat,
                    Kind = ConsoleItemKind.Command,
                    Extra = shortcut,
                    LowerName = name.ToLowerInvariant(),
                    LowerCategory = cat.ToLowerInvariant()
                });
            }

            // 3D Channel
            AddEff("3D Channel Extract", "ec.3d.channelextract", "3D Channel");
            AddEff("Cryptomatte", "ec.3d.cryptomatte", "3D Channel");
            AddEff("Depth Matte", "ec.3d.depthmatte", "3D Channel");
            AddEff("Depth of Field", "ec.3d.depthoffield", "3D Channel");
            AddEff("EXtractoR", "ec.3d.extractor", "3D Channel");
            AddEff("Fog 3D", "ec.3d.fog3d", "3D Channel");
            AddEff("ID Matte", "ec.3d.idmatte", "3D Channel");
            AddEff("IDentifier", "ec.3d.identifier", "3D Channel");

            // Audio
            AddEff("Backwards", "ec.audio.backwards", "Audio");
            AddEff("Bass & Treble", "ec.audio.basstreble", "Audio");
            AddEff("Compressor", "ec.audio.compressor", "Audio");
            AddEff("Delay", "ec.audio.delay", "Audio");
            AddEff("Distortion", "ec.audio.distortion", "Audio");
            AddEff("Flange & Chorus", "ec.audio.flangechorus", "Audio");
            AddEff("Gate", "ec.audio.gate", "Audio");
            AddEff("High-Low Pass", "ec.audio.highlowpass", "Audio");
            AddEff("Modulator", "ec.audio.modulator", "Audio");
            AddEff("Parametric EQ", "ec.audio.parametriceq", "Audio");
            AddEff("Reverb", "ec.audio.reverb", "Audio");
            AddEff("Stereo Mixer", "ec.audio.stereomixer", "Audio");
            AddEff("Tone", "ec.audio.tone", "Audio");

            // Blur & Sharpen
            AddEff("Bilateral Blur", "ec.blur.bilateral", "Blur & Sharpen");
            AddEff("CC Cross Blur", "ec.blur.cccross", "Blur & Sharpen");
            AddEff("CC Radial Blur", "ec.blur.ccradial", "Blur & Sharpen");
            AddEff("CC Radial Fast Blur", "ec.blur.ccradialfast", "Blur & Sharpen");
            AddEff("CC Vector Blur", "ec.blur.ccvector", "Blur & Sharpen");
            AddEff("Camera Lens Blur", "ec.blur.cameralens", "Blur & Sharpen");
            AddEff("Camera-Shake Deblur", "ec.blur.camerashakedeblur", "Blur & Sharpen");
            AddEff("Channel Blur", "ec.blur.channel", "Blur & Sharpen");
            AddEff("Compound Blur", "ec.blur.compound", "Blur & Sharpen");
            AddEff("Directional Blur", "ec.blur.directional", "Blur & Sharpen");
            AddEff("Fast Box Blur", "ec.blur.fastbox", "Blur & Sharpen", "Fast Blur, Box Blur");
            AddEff("Gaussian Blur", "ec.blur.gaussian", "Blur & Sharpen");
            AddEff("Radial Blur", "ec.blur.radial", "Blur & Sharpen");
            AddEff("Sharpen", "ec.blur.sharpen", "Blur & Sharpen");
            AddEff("Smart Blur", "ec.blur.smart", "Blur & Sharpen");
            AddEff("Unsharp Mask", "ec.blur.unsharp", "Blur & Sharpen");

            // Channel
            AddEff("Arithmetic", "ec.channel.arithmetic", "Channel");
            AddEff("Blend", "ec.channel.blend", "Channel");
            AddEff("CC Composite", "ec.channel.cccomposite", "Channel");
            AddEff("Calculations", "ec.channel.calculations", "Channel");
            AddEff("Channel Combiner", "ec.channel.combiner", "Channel");
            AddEff("Compound Arithmetic", "ec.channel.compoundarithmetic", "Channel");
            AddEff("Invert", "ec.channel.invert", "Channel");
            AddEff("Minimax", "ec.channel.minimax", "Channel");
            AddEff("Remove Color Matting", "ec.channel.removecolormatting", "Channel");
            AddEff("Set Channels", "ec.channel.setchannels", "Channel");
            AddEff("Set Matte", "ec.channel.setmatte", "Channel");
            AddEff("Shift Channels", "ec.channel.shiftchannels", "Channel");
            AddEff("Solid Composite", "ec.channel.solidcomposite", "Channel");

            // Color Correction
            AddEff("Auto Color", "ec.color.autocolor", "Color Correction");
            AddEff("Auto Contrast", "ec.color.autocontrast", "Color Correction");
            AddEff("Auto Levels", "ec.color.autolevels", "Color Correction");
            AddEff("Black & White", "ec.color.blackwhite", "Color Correction");
            AddEff("Brightness & Contrast", "ec.color.brightnesscontrast", "Color Correction");
            AddEff("Broadcast Colors", "ec.color.broadcast", "Color Correction");
            AddEff("CC Color Neutralizer", "ec.color.cccolorneutralizer", "Color Correction");
            AddEff("CC Color Offset", "ec.color.cccoloroffset", "Color Correction");
            AddEff("CC Kernel", "ec.color.cckernel", "Color Correction");
            AddEff("CC Toner", "ec.color.cctoner", "Color Correction");
            AddEff("Change Color", "ec.color.changecolor", "Color Correction");
            AddEff("Change to Color", "ec.color.changetocolor", "Color Correction");
            AddEff("Channel Mixer", "ec.color.channelmixer", "Color Correction");
            AddEff("Color Balance", "ec.color.colorbalance", "Color Correction");
            AddEff("Color Balance (HLS)", "ec.color.colorbalancehls", "Color Correction");
            AddEff("Color Link", "ec.color.colorlink", "Color Correction");
            AddEff("Color Stabilizer", "ec.color.colorstabilizer", "Color Correction");
            AddEff("Colorama", "ec.color.colorama", "Color Correction");
            AddEff("Curves", "ec.color.curves", "Color Correction");
            AddEff("Equalize", "ec.color.equalize", "Color Correction");
            AddEff("Exposure", "ec.color.exposure", "Color Correction");
            AddEff("Glow", "ec.stylize.glow", "Stylize");
            AddEff("Drop Shadow", "ec.perspective.dropshadow", "Perspective");
            AddEff("Hue/Saturation", "ec.color.huesaturation", "Color Correction");
            AddEff("Levels", "ec.color.levels", "Color Correction");
            AddEff("Levels (Individual Controls)", "ec.color.levelsic", "Color Correction");
            AddEff("Lumetri Color", "ec.color.lumetri", "Color Correction");
            AddEff("Photo Filter", "ec.color.photofilter", "Color Correction");
            AddEff("Selective Color", "ec.color.selectivecolor", "Color Correction");
            AddEff("Shadow/Highlight", "ec.color.shadowhighlight", "Color Correction");
            AddEff("Tint", "ec.color.tint", "Color Correction");
            AddEff("Tritone", "ec.color.tritone", "Color Correction");
            AddEff("Vibrance", "ec.color.vibrance", "Color Correction");

            // Distort
            AddEff("Bezier Warp", "ec.distort.bezierwarp", "Distort");
            AddEff("Bulge", "ec.distort.bulge", "Distort");
            AddEff("Corner Pin", "ec.distort.cornerpin", "Distort");
            AddEff("Displacement Map", "ec.distort.displacementmap", "Distort");
            AddEff("Liquify", "ec.distort.liquify", "Distort");
            AddEff("Magnify", "ec.distort.magnify", "Distort");
            AddEff("Mesh Warp", "ec.distort.meshwarp", "Distort");
            AddEff("Mirror", "ec.distort.mirror", "Distort");
            AddEff("Offset", "ec.distort.offset", "Distort");
            AddEff("Optics Compensation", "ec.distort.opticscompensation", "Distort");
            AddEff("Polar Coordinates", "ec.distort.polar", "Distort");
            AddEff("Reshape", "ec.distort.reshape", "Distort");
            AddEff("Ripple", "ec.distort.ripple", "Distort");
            AddEff("Spherize", "ec.distort.spherize", "Distort");
            AddEff("Transform", "ec.distort.transform", "Distort");
            AddEff("Turbulent Displace", "ec.distort.turbulentdisplace", "Distort");
            AddEff("Twirl", "ec.distort.twirl", "Distort");
            AddEff("Warp", "ec.distort.warp", "Distort");
            AddEff("Wave Warp", "ec.distort.wavewarp", "Distort");

            // Generate
            AddEff("Advanced Lightning", "ec.generate.advancedlightning", "Generate");
            AddEff("Audio Spectrum", "ec.generate.audiospectrum", "Generate");
            AddEff("Audio Waveform", "ec.generate.audiowaveform", "Generate");
            AddEff("Beam", "ec.generate.beam", "Generate");
            AddEff("Cell Pattern", "ec.generate.cellpattern", "Generate");
            AddEff("Checkerboard", "ec.generate.checkerboard", "Generate");
            AddEff("Circle", "ec.generate.circle", "Generate");
            AddEff("Ellipse", "ec.generate.ellipse", "Generate");
            AddEff("Eyedropper Fill", "ec.generate.eyedropperfill", "Generate");
            AddEff("Fill", "ec.generate.fill", "Generate");
            AddEff("Fractal", "ec.generate.fractal", "Generate");
            AddEff("Gradient Ramp", "ec.generate.gradientramp", "Generate");
            AddEff("Grid", "ec.generate.grid", "Generate");
            AddEff("Lens Flare", "ec.generate.lensflare", "Generate");
            AddEff("Paint Bucket", "ec.generate.paintbucket", "Generate");
            AddEff("Radio Waves", "ec.generate.radiowaves", "Generate");
            AddEff("Stroke", "ec.generate.stroke", "Generate");
            AddEff("Vegas", "ec.generate.vegas", "Generate");
            AddEff("Write-on", "ec.generate.writeon", "Generate");

            // Keying
            AddEff("Linear Color Key", "ec.key.linearcolor", "Keying");
            AddEff("Color Difference Key", "ec.key.colordifference", "Keying");
            AddEff("Color Range", "ec.key.colorrange", "Keying");
            AddEff("Difference Matte", "ec.key.differencematte", "Keying");
            AddEff("Extract", "ec.key.extract", "Keying");
            AddEff("Key Cleaner", "ec.key.keycleaner", "Keying");
            AddEff("Key Light", "ec.keying.keylight", "Keying", "Keylight, Keylight 1.2");
            AddEff("Spill Suppressor", "ec.key.spill", "Keying");

            // Stylize
            AddEff("Motion Tile", "ec.stylize.motiontile", "Stylize");
            AddEff("Posterize", "ec.stylize.posterize", "Stylize");
            AddEff("Roughen Edges", "ec.stylize.roughenedges", "Stylize", "Rough Edges");
            AddEff("Scatter", "ec.stylize.scatter", "Stylize");
            AddEff("Strobe Light", "ec.stylize.strobe", "Stylize");
            AddEff("Threshold", "ec.stylize.threshold", "Stylize");

            // Presets
            AddPre("Typewriter", "typewriter", "Text Animation");
            AddPre("Fade Up Characters", "fadeUpCharacters", "Text Animation");
            AddPre("Bounce In Words", "bounceInWords", "Text Animation");
            AddPre("Tracking In", "trackingIn", "Text Animation");
            AddPre("Scramble", "scramble", "Text Animation");
            AddPre("Blur In", "blurIn", "Text Animation");
            AddPre("Jitter", "jitter", "Text Animation");
            AddPre("Drop In Lines", "dropInLines", "Text Animation");

            // Layers
            AddLayer("New Solid...", "layer.newSolid", "Layer", "Ctrl+Y");
            AddLayer("New Null Object", "layer.newNull", "Layer", "Ctrl+Alt+Shift+Y");
            AddLayer("New Adjustment Layer", "layer.newAdjustment", "Layer", "Ctrl+Alt+Y");
            AddLayer("New Text Layer", "layer.newText", "Layer", "Ctrl+Alt+Shift+T");
            AddLayer("New Shape Layer", "layer.newShape", "Layer");
            AddLayer("New Camera...", "layer.newCamera", "Layer", "Ctrl+Alt+Shift+C");
            AddLayer("New Light...", "layer.newLight", "Layer", "Ctrl+Alt+Shift+L");

            // Commands
            AddCommand("New Composition...", "comp.new", "Composition", "Ctrl+N");
            AddCommand("Composition Settings...", "comp.settings", "Composition", "Ctrl+K");
            AddCommand("Duplicate", "edit.duplicate", "Edit", "Ctrl+D");
            AddCommand("Split Layer", "edit.splitLayer", "Edit", "Ctrl+Shift+D");
            AddCommand("Trim Comp to Work Area", "comp.trimWorkArea", "Composition", "Ctrl+Shift+X");
            AddCommand("Add to Render Queue", "renderQueue.add", "Composition", "Ctrl+M");
            AddCommand("Purge All Memory & Disk Cache", "edit.purge", "Edit");

            return list;
        }
    }
}
