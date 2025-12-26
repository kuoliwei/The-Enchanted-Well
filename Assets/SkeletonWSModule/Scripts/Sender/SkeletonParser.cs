using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace PoseSocket
{
    public static class SkeletonParser
    {
        public static SkeletonFrame Parse(string json)
        {
            var root = JObject.Parse(json);
            SkeletonFrame frame = new SkeletonFrame();

            // 1. frame index
            frame.frameIndex = root["frame_index"]?.Value<int>() ?? -1;

            // 2. persons
            var skeletons = root["skeletons"] as JArray;
            if (skeletons != null)
            {
                foreach (var personArr in skeletons)
                    frame.persons.Add(ParsePerson(personArr as JArray));
            }

            // 3. skeleton percentage
            var sp = root["skeleton_percentage"] as JArray;
            if (sp != null)
            {
                foreach (var v in sp)
                    frame.skeletonPercent.Add(v.Value<float>());
            }

            // 4. angle
            var angle = root["angle"] as JArray;
            if (angle != null)
            {
                foreach (var v in angle)
                    frame.angles.Add(v.Value<float>());
            }

            frame.recvTime = Time.time;
            return frame;
        }

        private static PersonSkeleton ParsePerson(JArray arr)
        {
            PersonSkeleton p = new PersonSkeleton();

            if (arr == null)
                return p;

            for (int i = 0; i < PoseSchema.JointCount; i++)
            {
                if (i >= arr.Count)
                {
                    p.joints[i] = new Joint(0, 0, 0, 0);
                    continue;
                }

                var j = arr[i] as JArray;
                if (j == null || j.Count < 3)
                {
                    p.joints[i] = new Joint(0, 0, 0, 0);
                    continue;
                }

                float x = j[0].Value<float>();
                float y = j[1].Value<float>();
                float z = j[2].Value<float>();
                float c = j.Count >= 4 ? j[3].Value<float>() : 1f;

                p.joints[i] = new Joint(x, y, z, c);
            }

            return p;
        }
    }
}
