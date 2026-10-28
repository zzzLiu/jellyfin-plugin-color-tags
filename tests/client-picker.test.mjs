import { readFileSync } from "node:fs";
import { createContext, runInContext } from "node:vm";
import { test } from "node:test";
import assert from "node:assert/strict";

const source = readFileSync(new URL("../src/Jellyfin.Plugin.ColorDots/Web/client.js", import.meta.url), "utf8");
const sandbox = {
	document: {
		currentScript: null,
		querySelector() {
			return null;
		},
		documentElement: { dataset: {} },
		readyState: "complete"
	},
	globalThis: null
};
sandbox.globalThis = sandbox;
const context = createContext(sandbox);
let api;
sandbox.__JFCD_TEST = (value) => {
	api = value;
};
runInContext(source, context, { filename: "client.js" });

api.setLabels([
	{ id: 0, name: "White", color: "#f4f4f5" },
	{ id: 2, name: "看过", color: "#3cba54" },
	{ id: 6, name: "强制转码", color: "#ff6a00" },
	{ id: 3, name: "要下", color: "#3b82f6" }
]);

function host(ids) {
	return Array.from(ids);
}

test("one label is a solid color and none is white", () => {
	assert.equal(api.dotFill([]), "#f4f4f5");
	assert.equal(api.dotFill([2]), "#3cba54");
	assert.deepEqual(host(api.normLabels([0, 2, 2, "nope"])), [2]);
});

test("several labels share equal conic slices in label order", () => {
	const fill = api.dotFill(api.normLabels([6, 2]));
	assert.equal(fill, "conic-gradient(#3cba54 0% 50%, #ff6a00 50% 100%)");
});

test("toggle adds and removes without closing the set", () => {
	assert.deepEqual(host(api.toggled([2], 6)), [2, 6]);
	assert.deepEqual(host(api.toggled([2, 6], 2)), [6]);
});
