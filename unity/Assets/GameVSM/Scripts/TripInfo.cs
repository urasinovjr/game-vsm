using System.Linq;
using UnityEngine;

namespace GameVSM
{
    public enum TripStatus { Boarding, Departure, Arrived }
    public enum SignKind { Board, Carriage, Platform, Stela, Exit, ExitPortal }

    // A navigation sign in station space. Front face is read by someone walking towards
    // the terminal (-X); Arrow lies in the sign plane, +Y meaning "straight ahead".
    public readonly struct NavSign
    {
        public readonly SignKind Kind;
        public readonly Vector3 Position;
        public readonly bool Exit;
        public NavSign(SignKind kind, Vector3 position, bool exit = false) { Kind = kind; Position = position; Exit = exit; }
    }

    // Track and platform numbers of one station. Tracks lie every 12 m along Z, the trip's
    // train on Z = 0, platform axes at Z = -6 + 12k. Direction is the numbering step towards +Z.
    public sealed class TripStop
    {
        public readonly string Location, Station;
        public readonly int Track, Platform, Direction;
        public readonly Vector3 ExitGroup;
        public readonly NavSign[] Signs;
        public TripStop(string location, string station, int track, int platform, int direction, Vector3 exitGroup, NavSign[] signs)
        {
            Location = location; Station = station; Track = track; Platform = platform;
            Direction = direction; ExitGroup = exitGroup; Signs = signs;
        }
        public int TrackAt(float z) => Track + Direction * Mathf.RoundToInt(z / 12);
        public int PlatformAt(float axisZ) => Platform + Direction * Mathf.RoundToInt((axisZ + 6) / 12);
        public int ExitMentions => Signs.Count(s => s.Kind is SignKind.Exit or SignKind.ExitPortal || s.Exit);
    }

    // One study trip for every station sign and board. The numbers are a training assignment
    // on a reconstructed track layout, not a real timetable or track allocation.
    public static class TripInfo
    {
        public const string Number = "101", Name = "БЕЛЫЙ КРЕЧЕТ";
        public const string Origin = "САНКТ-ПЕТЕРБУРГ", OriginFrom = "САНКТ-ПЕТЕРБУРГА", Destination = "МОСКВА";
        public const string ExitText = "ВЫХОД В ГОРОД";
        public const int Carriage = 3;
        // Door of car 3 used for boarding and alighting (ShiftExperience entry target).
        public static readonly Vector3 CarriageDoor = new(47.5f, 1.3f, -2.7f);

        public static readonly TripStop Petersburg = new("Moskovsky", "МОСКОВСКИЙ ВОКЗАЛ", 4, 4, -1, new Vector3(-142.5f, 1.28f, -6), new[]
        {
            new NavSign(SignKind.Board, new Vector3(-34, 4.9f, -6)), new NavSign(SignKind.Board, new Vector3(98, 4.9f, -6)),
            new NavSign(SignKind.Carriage, new Vector3(46.2f, 3.75f, -3.2f)),
            new NavSign(SignKind.Platform, new Vector3(-106, 4.1f, -6)), new NavSign(SignKind.Platform, new Vector3(-70, 4.1f, -6)),
            new NavSign(SignKind.Platform, new Vector3(-10, 4.1f, -6)), new NavSign(SignKind.Platform, new Vector3(62, 4.1f, -6)),
            new NavSign(SignKind.Exit, new Vector3(14, 4.2f, -6)), new NavSign(SignKind.Exit, new Vector3(-82, 4.2f, -6)),
            new NavSign(SignKind.ExitPortal, new Vector3(-141.98f, 5.75f, -6)),
        });
        public static readonly TripStop Moscow = new("Leningradsky", "ЛЕНИНГРАДСКИЙ ВОКЗАЛ", 3, 2, 1, new Vector3(-142.5f, 1.28f, -6), new[]
        {
            new NavSign(SignKind.Board, new Vector3(-19, 5, -6)), new NavSign(SignKind.Board, new Vector3(89, 5, -6)),
            new NavSign(SignKind.Carriage, new Vector3(46.2f, 3.75f, -3.2f)),
            new NavSign(SignKind.Stela, new Vector3(-73, 1.28f, -7.2f), true), new NavSign(SignKind.Stela, new Vector3(-37, 1.28f, -7.2f)),
            new NavSign(SignKind.Stela, new Vector3(-1, 1.28f, -7.2f), true), new NavSign(SignKind.Stela, new Vector3(71, 1.28f, -7.2f), true),
            new NavSign(SignKind.Stela, new Vector3(107, 1.28f, -7.2f)),
            new NavSign(SignKind.Exit, new Vector3(-86, 5.3f, -6)),
            new NavSign(SignKind.ExitPortal, new Vector3(-140.76f, 5.1f, -6)),
        });

        public static TripStop At(string location) =>
            location == Moscow.Location ? Moscow : location == Petersburg.Location ? Petersburg : null;

        // Moscow only ever shows our train as arrived: it never advertises a return departure.
        public static TripStatus Status(string location, string node) =>
            location == Moscow.Location ? TripStatus.Arrived : node == "depart" ? TripStatus.Departure : TripStatus.Boarding;

        public static string StatusText(TripStatus status) => status switch
        {
            TripStatus.Departure => "ОТПРАВЛЕНИЕ",
            TripStatus.Arrived => "ПРИБЫЛ",
            _ => "ПОСАДКА"
        };

        public static string Text(SignKind kind, string location, string node, bool back = false)
        {
            var stop = At(location);
            var status = Status(location, node);
            string route = status == TripStatus.Arrived ? "ПРИБЫЛ ИЗ " + OriginFrom : Destination + "  ·  " + StatusText(status);
            return kind switch
            {
                SignKind.Board => $"{Number}  {Name}\n{route}\nПУТЬ {stop.Track}",
                SignKind.Carriage => $"ВАГОН {Carriage}",
                SignKind.Platform => PlatformLabel(stop, -6, back),
                SignKind.Stela => $"{Number}\n{Name}\n" + (status == TripStatus.Arrived ? "ПРИБЫЛ ИЗ\n" + OriginFrom : Destination + "\n" + StatusText(status)),
                _ => ExitText
            };
        }

        // Tracks on the reader's left and right. Reading the front face one looks along -X,
        // so -Z is on the left; the back face swaps sides instead of mirroring the text.
        public static string PlatformLabel(TripStop stop, float axisZ, bool back)
        {
            int left = stop.TrackAt(axisZ + (back ? 6 : -6)), right = stop.TrackAt(axisZ + (back ? -6 : 6));
            return $"ПУТЬ {left}   |   ПЛАТФОРМА {stop.PlatformAt(axisZ)}   |   ПУТЬ {right}";
        }
    }
}
