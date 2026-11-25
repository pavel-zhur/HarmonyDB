export class BlocksChartParameters {
    constructor(init: { typesToo?: boolean; groupNormalized?: boolean; showJoints?: boolean; showPath?: boolean } = {}) {
        this.typesToo = init.typesToo ?? true;
        this.groupNormalized = init.groupNormalized ?? true;
        this.showJoints = init.showJoints ?? true;
        this.showPath = init.showPath ?? true;
    }

    readonly typesToo: boolean;
    readonly groupNormalized: boolean;
    readonly showJoints: boolean;
    readonly showPath: boolean;
}
