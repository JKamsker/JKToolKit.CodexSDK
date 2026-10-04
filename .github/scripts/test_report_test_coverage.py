import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


spec = importlib.util.spec_from_file_location("report_test_coverage", Path(__file__).with_name("report_test_coverage.py"))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)


class CoverageReportTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def write(self, name, documents):
        path = self.root / name
        path.write_text(json.dumps({"JKToolKit.CodexSDK.dll": documents}))
        return path

    @staticmethod
    def method(hits, branch_hits=(0, 1)):
        return {"Example": {"Example::Run()": {"Lines": {"10": hits}, "Branches": [
            {"Line": 10, "Offset": 4, "EndOffset": 6 + index, "Path": index, "Ordinal": index, "Hits": value}
            for index, value in enumerate(branch_hits)
        ]}}}

    def test_duplicate_reports_do_not_inflate_lines_or_branches(self):
        path = self.write("a.json", {"/repo/src/JKToolKit.CodexSDK/A.cs": self.method(1)})
        result = reporter.summarize([path, path])["handwritten"]
        self.assertEqual((1, 1, 1, 2), tuple(result[k] for k in
            ("lines_covered", "lines_total", "branches_covered", "branches_total")))

    def test_integration_and_unit_reports_union_complementary_paths(self):
        first = self.write("unit.json", {"/repo/src/JKToolKit.CodexSDK/A.cs": self.method(0, (0, 1))})
        second = self.write("integration.json", {r"C:\repo\src\JKToolKit.CodexSDK\A.cs": self.method(1, (1, 0))})
        result = reporter.summarize([first, second])
        self.assertEqual(1, len(result["files"]))
        self.assertEqual(100, result["handwritten"]["lines_percent"])
        self.assertEqual(100, result["handwritten"]["branches_percent"])

    def test_checkout_under_another_src_directory_keeps_one_source_identity(self):
        first = self.write("linux.json", {"/home/dev/src/repo/src/JKToolKit.CodexSDK/A.cs": self.method(0, (0, 1))})
        second = self.write("windows.json", {r"D:\repo\src\JKToolKit.CodexSDK\A.cs": self.method(1, (1, 0))})
        result = reporter.summarize([first, second])
        self.assertEqual(["src/JKToolKit.CodexSDK/A.cs"], [row["file"] for row in result["files"]])
        self.assertEqual(1, result["handwritten"]["lines_total"])
        self.assertEqual(100, result["handwritten"]["branches_percent"])

    def test_generated_misses_remain_visible_without_lowering_handwritten_score(self):
        path = self.write("a.json", {
            "/repo/src/JKToolKit.CodexSDK/A.cs": self.method(1, (1, 1)),
            "/repo/src/JKToolKit.CodexSDK/Generated/Dto.g.cs": self.method(0, (0, 0)),
            "/repo/src/JKToolKit.CodexSDK/obj/Regex.g.cs": self.method(0, (0, 0)),
        })
        result = reporter.summarize([path])
        self.assertEqual(100, result["handwritten"]["lines_percent"])
        self.assertEqual(0, result["generated"]["lines_percent"])
        self.assertEqual(2, result["generated"]["lines_total"])
        self.assertEqual(1, result["generated_protocol"]["lines_total"])
        self.assertEqual(1, result["generated_build"]["lines_total"])

    def test_platform_generated_method_variants_do_not_inflate_protocol_totals(self):
        first = self.write("linux.json", {
            "/repo/src/JKToolKit.CodexSDK/Generated/Dto.g.cs": self.method(1),
            "/repo/src/JKToolKit.CodexSDK/obj/Regex.g.cs": self.method(1),
        })
        variant = {"PlatformSpecificGeneratedName": self.method(1)["Example"]}
        second = self.write("windows.json", {
            "/repo/src/JKToolKit.CodexSDK/Generated/Dto.g.cs": self.method(1),
            "/repo/src/JKToolKit.CodexSDK/obj/Regex.g.cs": variant,
        })
        result = reporter.summarize([first, second])
        self.assertEqual(2, result["generated_protocol"]["branches_total"])
        self.assertEqual(4, result["generated_build"]["branches_total"])

    def test_multiple_conditions_on_one_line_remain_distinct(self):
        methods = self.method(1)
        method = methods["Example"]["Example::Run()"]
        method["Branches"].append({"Line": 10, "Offset": 20, "EndOffset": 30, "Path": 0, "Ordinal": 0, "Hits": 0})
        result = reporter.summarize([self.write("a.json", {"/repo/src/JKToolKit.CodexSDK/A.cs": methods})])
        self.assertEqual(3, result["handwritten"]["branches_total"])
        self.assertEqual([10], result["files"][0]["uncovered_branch_lines"])

    def test_empty_or_wrong_module_report_is_rejected(self):
        path = self.root / "empty.json"
        path.write_text(json.dumps({"Unrelated.dll": {}}))
        with self.assertRaisesRegex(ValueError, "No SDK coverage"):
            reporter.summarize([path])

    def test_incomplete_sdk_report_cannot_pass_whole_sdk_gate(self):
        path = self.write("a.json", {"/repo/src/JKToolKit.CodexSDK/A.cs": self.method(1)})
        with self.assertRaisesRegex(ValueError, "missing SDK assemblies.*AgentFramework"):
            reporter.summarize([path], require_all_modules=True)

    def test_windows_module_path_is_recognized(self):
        path = self.root / "windows.json"
        path.write_text(json.dumps({r"C:\repo\bin\JKToolKit.CodexSDK.dll": {
            r"C:\repo\src\JKToolKit.CodexSDK\A.cs": self.method(1)}}))
        result = reporter.summarize([path])
        self.assertEqual(1, result["handwritten"]["lines_covered"])
        self.assertIn("JKToolKit.CodexSDK.dll", result["modules"])

    def test_empty_adapter_documents_do_not_satisfy_required_modules(self):
        path = self.root / "empty-adapters.json"
        path.write_text(json.dumps({
            "JKToolKit.CodexSDK.dll": {"/repo/src/JKToolKit.CodexSDK/A.cs": self.method(1)},
            "JKToolKit.CodexSDK.AgentFramework.dll": {"/repo/src/JKToolKit.CodexSDK.AgentFramework/A.cs": {}},
            "JKToolKit.CodexSDK.SemanticKernel.dll": {"/repo/src/JKToolKit.CodexSDK.SemanticKernel/A.cs": {
                "Example": {"Example::Run()": {"Lines": {}, "Branches": []}}}},
        }))
        with self.assertRaisesRegex(ValueError, "missing SDK assemblies"):
            reporter.summarize([path], require_all_modules=True)

    def test_generated_only_sdk_cannot_pass_handwritten_gate(self):
        path = self.root / "generated-only-sdk.json"
        path.write_text(json.dumps({
            "JKToolKit.CodexSDK.dll": {"/repo/src/JKToolKit.CodexSDK/Generated/A.g.cs": self.method(1)},
            "JKToolKit.CodexSDK.AgentFramework.dll": {"/repo/src/JKToolKit.CodexSDK.AgentFramework/A.cs": self.method(1)},
            "JKToolKit.CodexSDK.SemanticKernel.dll": {"/repo/src/JKToolKit.CodexSDK.SemanticKernel/A.cs": self.method(1)},
        }))
        with self.assertRaisesRegex(ValueError, "missing SDK assemblies with handwritten lines: JKToolKit.CodexSDK.dll"):
            reporter.summarize([path], require_all_modules=True)

    def test_outside_source_is_rejected_instead_of_hidden(self):
        path = self.write("a.json", {"/unexpected/A.cs": self.method(1)})
        with self.assertRaisesRegex(ValueError, "outside src"):
            reporter.summarize([path])


if __name__ == "__main__":
    unittest.main()
