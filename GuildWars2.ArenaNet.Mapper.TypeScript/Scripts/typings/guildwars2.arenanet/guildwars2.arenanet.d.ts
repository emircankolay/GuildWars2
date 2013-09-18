declare module GuildWars2.ArenaNet.Model {

    export interface EventDetails {
        name: string;
        level: number;
        map_id: number;
        flags: EventFlagType[];
        location: Location;
    }

    export interface EventDetailsResponse {
        events: { [key: string]: EventDetails };
    }

    export enum EventFlagType {
        group_event,
        map_wide
    }
    
    export interface EventsResponse {
        events: EventState[];
    }

    export interface EventState {
        world_id: number;
        map_id: number;
        event_id: string;
        state: EventStateType;
    }

    export enum EventStateType {
        Active,
        Success,
        Fail,
        Warmup,
        Inactive,
        Preparation 
    }

    export interface Floor {
        texture_dims: number[];
        regions: { [key: string]: FloorRegion };
    }

    export interface FloorMapDetails {
        name: string;
        min_level: number;
        max_level: number;
        default_floor: number;
        map_rect: number[][];
        continent_rect: number[][];
        points_of_interest: PointOfInterest[];
        tasks: Task[];
        skill_challenges: MappedModel[];
        sectors: Sector[];
    }

    export interface FloorRegion {
        name: string;
        label_coord: number[];
        maps: { [key: string]: FloorMapDetails };
    }

    export interface Location {
        type: LocationType;

        /* cylinder / sphere values */
        radius?: number;
        rotation?: number;

        /* cylinder only values */
        height?: number;

        /* poly only values */
        z_range?: number[];
        points?: number[][];
    }

    export enum LocationType {
        cylinder,
        poly,
        sphere
    }

    export interface Map extends NamedModel < number > { }

    export interface MappedModel {
        coord: number[];
    }

    export interface MapFloorResponse extends Floor { }

    export interface MapNamesResponse extends Array < Map > { }

    export interface NamedModel < T > {
        id: T;
        name: string;
    }

    export interface PointOfInterest extends MappedModel {
        poi_id: number;
        name: string;
        type: PointOfInterestType;
        floor: number;
    }

    export enum PointOfInterestType {
        landmark,
        unlock,
        vista,
        waypoint
    }

    export interface Sector extends MappedModel {
        sector_id: number;
        name: string;
        level: number;
    }

    export interface Task extends MappedModel {
        task_id: number;
        objective: string;
        level: number;
    }

}